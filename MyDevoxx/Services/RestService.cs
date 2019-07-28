using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.Web.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using MyDevoxx.Services.RestModel;
using Windows.Storage;
using System.Threading;
using MyDevoxx.Converter.Model;
using SQLite.Net;
using SQLite.Net.Async;
using MyDevoxx.Services.RestModel.Voting;
using MyDevoxx.Utils;
using Windows.Storage.Streams;

namespace MyDevoxx.Services
{
    public class RestService : IRestService
    {
        private CancellationTokenSource cts;
        private StorageFolder localFolder = ApplicationData.Current.LocalFolder;
        private ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
        private SQLiteAsyncConnection sqlConnection;

        private string cfpUrl = "https://s3-eu-west-1.amazonaws.com/cfpdevoxx/cfp.json";
        private string speakersUrl = "/speakers";
        private string tracksUrl = "/tracks";
        private string scheduleUrl = "/schedules";

        private const int CACHE_LIVETIME_IN_HOURS = 6;
        private const int CACHE_LIVETIME_WITHOUT_PUSH_IN_MINUTES = 15;

        public RestService()
        {
            cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            DBConnection();
        }

        private HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            // Add a user-agent header 
            var headers = client.DefaultRequestHeaders;
            headers.UserAgent.ParseAdd("ie");
            headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)");
            headers.Accept.ParseAdd("application/json");

            return client;
        }

        public async Task<Tuple<List<Model.Event>, bool>> GetEvents(string day)
        {
            string endpoint = (string)settings.Values[Settings.CFP_ENDPOINT];
            string confId = (string)settings.Values[Settings.CONFERENCE_ID];
            string country = (string)settings.Values[Settings.COUNTRY];

            string url = endpoint + "/conferences" + "/" + confId + scheduleUrl + "/" + day + "/";
            Tuple<Schedule, bool> result = await fetch<Schedule>(url, "schedule_" + day + "_" + confId + ".json");

            return Tuple.Create(EventConverter.apply(result.Item1, confId, country, day), result.Item2);
        }

        public async Task<Tuple<List<Model.Speaker>, bool>> GetSpeakers()
        {
            string endpoint = (string)settings.Values[Settings.CFP_ENDPOINT];
            string confId = (string)settings.Values[Settings.CONFERENCE_ID];
            string url = endpoint + "/conferences" + "/" + confId + speakersUrl;
            Tuple<List<Speaker>, bool> result = await fetch<List<Speaker>>(url, "speakerlist_" + confId + ".json");
            List<Model.Speaker> speakerList = new List<Model.Speaker>();
            foreach (Speaker s in result.Item1)
            {
                speakerList.Add(SpeakerConverter.apply(s, confId));
            }
            return Tuple.Create(speakerList, result.Item2);
        }

        public async Task<Tuple<Model.Speaker, bool>> GetSpeaker(string uuid)
        {
            string endpoint = (string)settings.Values[Settings.CFP_ENDPOINT];
            string confId = (string)settings.Values[Settings.CONFERENCE_ID];
            string url = endpoint + "/conferences" + "/" + confId + speakersUrl + "/" + uuid;
            Tuple<Speaker, bool> result = await fetch<Speaker>(url, "speaker_" + uuid + "_" + confId + ".json");
            return Tuple.Create(SpeakerConverter.apply(result.Item1, confId), result.Item2);
        }

        public async Task<Tuple<List<Model.Track>, bool>> GetTracks()
        {
            string endpoint = (string)settings.Values[Settings.CFP_ENDPOINT];
            string confId = (string)settings.Values[Settings.CONFERENCE_ID];
            string url = endpoint + "/conferences" + "/" + confId + tracksUrl;
            Tuple<TrackList, bool> result = await fetch<TrackList>(url, "tracksList_" + confId + ".json");

            return Tuple.Create(TrackListConverter.apply(result.Item1, confId), result.Item2);
        }

        public async Task<Tuple<List<Model.Conference>, bool>> GetConferences()
        {
            Tuple<List<CFP>, bool> cfp = await fetch<List<CFP>>(cfpUrl, "cfp.json");
            List<Model.Conference> conferences = new List<Model.Conference>();
            foreach (CFP c in cfp.Item1)
            {
                conferences.Add(ConferenceConverter.apply(c));
            }

            return Tuple.Create(conferences, cfp.Item2);
        }

        public async Task<Tuple<List<Model.Floor>, bool>> GetFloors()
        {
            string confId = (string)settings.Values[Settings.CONFERENCE_ID];
            Tuple<List<CFP>, bool> cfp = await fetch<List<CFP>>(cfpUrl, "cfp.json");
            foreach (CFP c in cfp.Item1)
            {
                if (c.id.Equals(confId))
                {
                    return Tuple.Create(FloorConverter.apply(c), cfp.Item2);
                }
            }

            return Tuple.Create(new List<Model.Floor>(), cfp.Item2);
        }

        private async Task<Tuple<T, bool>> fetch<T>(string url, string cacheFileName)
        {
            Debug.WriteLine("Requested URL: " + url);

            HttpClient client = CreateHttpClient();

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
            Uri resourceUri = new Uri(url, UriKind.Absolute);
            string responseText;
            Model.ETag eTag;
            bool updated = false;

            client.DefaultRequestHeaders.Remove("If-None-Match");
            eTag = await GetEtag(url);
            if (eTag != null && eTag.eTag != null)
            {
                client.DefaultRequestHeaders.Add(new KeyValuePair<string, string>("If-None-Match", eTag.eTag));
            }

            try
            {
                bool pushEnabled;
                if (settings.Values[Settings.PUSH_UPDATE_ENABLED] == null
                    || !Boolean.TryParse((string)settings.Values[Settings.PUSH_UPDATE_ENABLED], out pushEnabled))
                {
                    pushEnabled = false;
                }
                bool isCacheInvalid = eTag == null
                    || ((DateTime.Now - eTag.timeStamp).TotalHours >= CACHE_LIVETIME_IN_HOURS && pushEnabled)
                    || ((DateTime.Now - eTag.timeStamp).TotalMinutes >= CACHE_LIVETIME_WITHOUT_PUSH_IN_MINUTES && !pushEnabled);

                if (isCacheInvalid)
                {
                    //last request was more than 10 minutes / 6 hours ago
                    HttpResponseMessage msg = await client.GetAsync(resourceUri);
                    if (HttpStatusCode.NotModified.Equals(msg.StatusCode))
                    {
                        //content has not changed since last request
                        responseText = await loadFromFile(cacheFileName);
                        SaveEtag(url, eTag.eTag);
                        Debug.WriteLine("from Cache because not modified");
                    }
                    else
                    {
                        //content is new or has changed since last request
                        responseText = await msg.Content.ReadAsStringAsync();
                        await saveToFile(cacheFileName, responseText);

                        string newEtag;
                        msg.Headers.TryGetValue("Etag", out newEtag);
                        SaveEtag(url, newEtag);
                        updated = true;
                        Debug.WriteLine("from Server");
                    }
                }
                else
                {
                    //last request was less than CACHE_LIVETIME_IN_HOURS ago
                    responseText = await loadFromFile(cacheFileName);
                    Debug.WriteLine("from Cache");
                }

                return Tuple.Create((T)serializer.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(responseText))), updated);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                responseText = await loadFromFile(cacheFileName);
                if (responseText == null)
                {
                    responseText = await loadFromFallbackFile(cacheFileName);
                    Debug.WriteLine("Read fallback file!");
                }
                if (responseText != null)
                {
                    return Tuple.Create((T)serializer.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(responseText))), false);
                }
            }
            return Tuple.Create(Activator.CreateInstance<T>(), true);
        }

        private async Task saveToFile(string filename, string data)
        {
            StorageFile file = await localFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteTextAsync(file, data);
        }

        private async Task<string> loadFromFile(string filename)
        {
            try
            {
                StorageFile file = await localFolder.GetFileAsync(filename);
                return await FileIO.ReadTextAsync(file);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<string> loadFromFallbackFile(string filename)
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(@"ms-appx:///Assets/FallbackData/" + filename));
                return await FileIO.ReadTextAsync(file);
            }
            catch (Exception)
            {
                Debug.WriteLine("Fallback file not found " + filename);
                return null;
            }
        }

        private async Task<Model.ETag> GetEtag(string url)
        {
            Model.ETag result = await sqlConnection.Table<Model.ETag>().Where(i => i.url.Equals(url)).FirstOrDefaultAsync();
            if (result != null)
            {
                Debug.WriteLine(result.eTag + " - " + result.url + " created at " + result.timeStamp);
                return result;
            }
            return null;
        }

        private async void SaveEtag(string url, string eTag)
        {
            await sqlConnection.InsertOrReplaceAsync(new Model.ETag() { url = url, eTag = eTag, timeStamp = DateTime.Now });
        }

        private void DBConnection()
        {
            if (sqlConnection != null) return;

            var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "db.sqlite");

            SQLiteConnectionString connString = new SQLiteConnectionString(path.ToString(), false);
            SQLiteConnectionWithLock connLock = new SQLiteConnectionWithLock(new SQLite.Net.Platform.WinRT.SQLitePlatformWinRT(), connString);

            sqlConnection = new SQLiteAsyncConnection(() => connLock);
        }

        public async Task<VoteMessage> VoteTalk(VoteBasic vote)
        {
            return await Vote(vote);
        }

        public async Task<VoteMessage> VoteTalk(VoteReviews vote)
        {
            return await Vote(vote);
        }

        private async Task<VoteMessage> Vote<T>(T vote)
        {
            HttpClient client = CreateHttpClient();

            string confId = (string)settings.Values[Settings.CONFERENCE_ID];
            string votingURL = (string)settings.Values[Settings.VOTING_URL];

            MemoryStream stream = new MemoryStream();
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
            serializer.WriteObject(stream, vote);
            stream.Position = 0;
            StreamReader sr = new StreamReader(stream);
            string dataString = sr.ReadToEnd();

            try
            {
                HttpResponseMessage response = await client.PostAsync(new Uri(votingURL + "api/voting/v1/vote"), new HttpStringContent(dataString, Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json"));
                if (!HttpStatusCode.Created.Equals(response.StatusCode))
                {
                    string responseTxt = await response.Content.ReadAsStringAsync();
                    DataContractJsonSerializer obj = new DataContractJsonSerializer(typeof(VoteMessage));
                    VoteMessage message = obj.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(responseTxt))) as VoteMessage;
                    return message;
                }
            }
            catch (Exception)
            {
                VoteMessage message = new VoteMessage();
                message.message = "Could not reach server, please try again later.";
                return message;
            }
            return null;
        }

        public async Task<VoteResults> GetVoteResults(int limit, string day, string talkType, string track)
        {
            //[/DV15/top/talks{?limit}&{day}&{talkType}&{track}]
            HttpClient client = CreateHttpClient();
            string confId = (string)settings.Values[Settings.CONFERENCE_ID];
            string url = (string)settings.Values[Settings.VOTING_URL];
            string parameters = buildVoteParameters(limit, day, talkType, track);
            try
            {
                HttpResponseMessage response = await client.GetAsync(new Uri(url + confId + "/top/talks" + parameters));
                if (HttpStatusCode.Ok.Equals(response.StatusCode))
                {
                    string responseTxt = await response.Content.ReadAsStringAsync();
                    DataContractJsonSerializer obj = new DataContractJsonSerializer(typeof(VoteResults));
                    VoteResults result = obj.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(responseTxt))) as VoteResults;
                    return result;
                }
            }
            catch (Exception)
            {
                Debug.WriteLine("Could not reach server to get vote results!");
            }
            return new VoteResults();
        }

        private string buildVoteParameters(int limit, string day, string talkType, string track)
        {
            StringBuilder b = new StringBuilder("");
            if (limit > 0)
            {
                b.Append("?limit=").Append(limit);
            }
            if (!String.IsNullOrEmpty(day))
            {
                b.Append(getConChar(b)).Append("day=").Append(day);
            }
            if (!String.IsNullOrEmpty(talkType))
            {

                b.Append(getConChar(b)).Append("talkType=").Append(System.Net.WebUtility.UrlEncode(talkType));
            }
            if (!String.IsNullOrEmpty(track))
            {
                b.Append(getConChar(b)).Append("track=").Append(System.Net.WebUtility.UrlEncode(track));
            }

            return b.ToString();
        }

        private string getConChar(StringBuilder b)
        {
            if (b.Length == 0)
            {
                return "?";
            }
            else
            {
                return "&";
            }
        }

        public async Task<Categories> GetVoteCategories()
        {
            HttpClient client = CreateHttpClient();
            string confId = (string)settings.Values[Settings.CONFERENCE_ID];
            string url = (string)settings.Values[Settings.VOTING_URL];
            try
            {
                HttpResponseMessage response = await client.GetAsync(new Uri(url + confId + "/categories"));
                if (HttpStatusCode.Ok.Equals(response.StatusCode))
                {
                    string responseTxt = await response.Content.ReadAsStringAsync();
                    DataContractJsonSerializer obj = new DataContractJsonSerializer(typeof(Categories));
                    Categories result = obj.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(responseTxt))) as Categories;
                    return result;
                }
            }
            catch (Exception)
            { }
            return null;
        }

        //public async Task<VoteResult> getVoteForTalk(string talkId)
        //{
        //    string conference = (string)settings.Values["conference"];
        //    string url = Config.Instance.getConference(conference).voteUrl;
        //    try
        //    {
        //        HttpResponseMessage response = await httpClient.GetAsync(new Uri(url + voteResultTalkAddress));
        //        if (HttpStatusCode.Ok.Equals(response.StatusCode))
        //        {
        //            string responseTxt = await response.Content.ReadAsStringAsync();
        //            DataContractJsonSerializer obj = new DataContractJsonSerializer(typeof(VoteResult));
        //            VoteResult result = obj.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(responseTxt))) as VoteResult;
        //            return result;
        //        }
        //    }
        //    catch (Exception)
        //    { }
        //    return null;
        //}

        public async Task LoadImage(List<string> httpUrls, string folder)
        {
            string confId = (string)settings.Values[Settings.CONFERENCE_ID];
            var imageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(folder + confId, CreationCollisionOption.OpenIfExists);

            try
            {
                foreach (string httpUrl in httpUrls)
                {
                    Debug.WriteLine("Loading Image from: " + httpUrl);
                    var url = new Uri(httpUrl + "?bust=" + DateTime.Now.Millisecond);
                    var fileName = Path.GetFileName(url.LocalPath);

                    HttpClient client = CreateHttpClient();
                    IBuffer iBuffer = await client.GetBufferAsync(url);

                    var imageFile = await imageFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteBufferAsync(imageFile, iBuffer);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Could not update track images");
                Debug.WriteLine(ex.Message);
            }
        }

    }
}
