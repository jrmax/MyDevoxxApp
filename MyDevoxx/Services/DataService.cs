using MyDevoxx.Model;
using Microsoft.Practices.ServiceLocation;
using SQLite.Net;
using SQLite.Net.Async;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using MyDevoxx.Utils;
using GalaSoft.MvvmLight.Messaging;
using System.Globalization;
using System.Diagnostics;

namespace MyDevoxx.Services
{
    public class DataService : IDataService
    {
        private ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
        private SQLiteAsyncConnection sqlConnection;
        private IRestService Service;

        private string[] DayNames = { "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" };
        private string localStoragePath = "ms-appdata:///local/";
        private string trackImageFolder = "trackimages";

        public DataService()
        {
            DBConnection();
            Service = ServiceLocator.Current.GetInstance<IRestService>();
        }

        public async Task<List<Conference>> FetchConferences()
        {
            Tuple<List<Conference>, bool> conferencesTuple = await Service.GetConferences();
            await sqlConnection.InsertOrReplaceAllAsync(conferencesTuple.Item1);
            return conferencesTuple.Item1;
        }

        public async void CollectAll(FinishedLoading callback)
        {
            try
            {
                await CollectEvents();
                await CollectSpeakers();
                await CollectFloors();
                callback(true);
                settings.Values[Settings.LAST_UPDATE + currentConferenceId()] = DateTime.Now.ToString();
            }
            catch
            {
                callback(false);
            }
        }

        public void InitDB()
        {
            var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "db.sqlite");
            SQLiteConnection conn = new SQLiteConnection(new SQLite.Net.Platform.WinRT.SQLitePlatformWinRT(), path);

            conn.CreateTable<Conference>();
            conn.CreateTable<Event>();
            conn.CreateTable<Model.Floor>();
            conn.CreateTable<ETag>();
            conn.CreateTable<Speaker>();
            conn.CreateTable<Track>();
            conn.CreateTable<Note>();
            conn.CreateTable<Vote>();

            conn.Close();
        }

        public async void removeFavorites(FinishedLoading callback)
        {
            await sqlConnection.ExecuteAsync("update 'Event' set Starred = 0");
            callback(true);
        }

        public async void CheckUpdate(FinishedLoading callback = null)
        {
            if (settings.Values[Settings.CONFERENCE_ID] == null
                || settings.Values[Settings.LOADED_SUCCESSFUL] == null
                || !(bool)settings.Values[Settings.LOADED_SUCCESSFUL])
            {
                return;
            }

            if (settings.Values[Settings.FORCE_UPDATE] != null)
            {
                await ClearETag();
                settings.Values[Settings.FORCE_UPDATE] = null;
            }

            try
            {
                await FetchConferences();
                Conference conference = await GetCurrentConference();
                settings.Values[Settings.CONFERENCE_ID] = conference.id;
                settings.Values[Settings.CFP_ENDPOINT] = conference.cfpEndpoint;
                settings.Values[Settings.TALK_URL] = conference.talkURL;
                settings.Values[Settings.COUNTRY] = conference.country;
                settings.Values[Settings.VOTING_URL] = conference.votingURL;
                settings.Values[Settings.PUSH_UPDATE_ENABLED] = conference.pushEnabled;

                if (await CollectEvents())
                {
                    Messenger.Default.Send<MessageType>(MessageType.REQUEST_REFRESH_SCHEDULE);
                    Messenger.Default.Send<MessageType>(MessageType.REFRESH_TRACKS);
                    settings.Values[Settings.LAST_UPDATE + conference.id] = DateTime.Now.ToString();
                }
                if (await CollectSpeakers())
                {
                    Messenger.Default.Send<MessageType>(MessageType.REFRESH_SPEAKERS);
                }
                await CollectFloors();

                callback?.Invoke(true);
            }
            catch
            {
                callback?.Invoke(false);
            }
        }

        private async Task<bool> CollectEvents()
        {
            bool updated = false;

            string confId = currentConferenceId();
            Conference conference = await sqlConnection.Table<Conference>().Where(c => c.id.Equals(confId)).FirstOrDefaultAsync();
            string cfpUrl = conference.cfpURL;
            int lastIdx = conference.cfpURL.LastIndexOf('/');
            if (lastIdx + 1 == conference.cfpURL.Length)
            {
                cfpUrl = conference.cfpURL.Substring(0, conference.cfpURL.Length - 1);
            }

            Tuple<List<Track>, bool> tracksTuple = await Service.GetTracks();
            if (tracksTuple.Item2)
            {
                updated = true;
                await ClearTracks(currentConferenceId());
                await sqlConnection.InsertOrReplaceAllAsync(tracksTuple.Item1);

                // Preload track images
                List<string> trackImages = tracksTuple.Item1.Select(track => cfpUrl + track.imgsrc).ToList();
                await Service.LoadImage(trackImages, trackImageFolder);
            }
            var trackDic = tracksTuple.Item1.Select(track => new
            {
                Key = track.id,
                Value = track.imgsrc.Substring(track.imgsrc.LastIndexOf('/', track.imgsrc.Length - 1))
            })
                .Distinct()
                .ToDictionary(track => track.Key, track => track.Value, StringComparer.OrdinalIgnoreCase);

            DateTime fromDate = DateTime.ParseExact(conference.fromDate, "yyyy-MM-dTHH:mm:ss.000Z", CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal);
            DateTime toDate = DateTime.ParseExact(conference.toDate, "yyyy-MM-dTHH:mm:ss.000Z", CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal);
            int dayCnt = (int)(toDate - fromDate).TotalDays;
            int dayStart = (int)fromDate.DayOfWeek;

            List<Event> starredEvents = await GetStarredEvents();
            Dictionary<string, Event> starredEventsDic = starredEvents.ToDictionary(p => p.id, p => p);

            List<Event> events = new List<Event>();
            List<String> daysToClean = new List<String>();
            for (int i = dayStart; i <= dayStart + dayCnt; i++)
            {
                Tuple<List<Event>, bool> eventListTuple = await Service.GetEvents(DayNames[i]);
                if (eventListTuple.Item2)
                {
                    updated = true;
                    daysToClean.Add(DayNames[i]);
                    foreach (Event e in eventListTuple.Item1)
                    {
                        if (e.trackId != null)
                        {
                            String trackImage;
                            if (trackDic.TryGetValue(e.trackId, out trackImage))
                            {
                                e.trackImage = localStoragePath + trackImageFolder + confId + trackImage;
                            }
                        }
                        if (starredEventsDic.ContainsKey(e.id))
                        {
                            e.Starred = true;
                        }
                        events.Add(e);
                    }
                }
            }
            // delete all events
            foreach (String day in daysToClean)
            {
                await ClearEventsByDay(confId, day);
            }
            if (daysToClean.Count > 0)
            {
                await sqlConnection.InsertOrReplaceAllAsync(events);
            }

            return updated;
        }

        private async Task<bool> CollectFloors()
        {
            Tuple<List<Model.Floor>, bool> floorsTuple = await Service.GetFloors();
            await ClearFloors(currentConferenceId());
            await sqlConnection.InsertOrReplaceAllAsync(floorsTuple.Item1);
            return floorsTuple.Item2;
        }

        private async Task<bool> CollectSpeakers()
        {
            Tuple<List<Speaker>, bool> speakersTuple = await Service.GetSpeakers();
            if (speakersTuple.Item2)
            {
                await ClearSpeakers(currentConferenceId());
                await sqlConnection.InsertOrReplaceAllAsync(speakersTuple.Item1);
            }
            return speakersTuple.Item2;
        }

        private async Task ClearEventsByDay(string confId, string day)
        {
            await sqlConnection.ExecuteAsync("delete from 'Event' where confId = '" + confId + "' and day = '" + day + "'");
        }

        private async Task ClearSpeakers(string confId)
        {
            await sqlConnection.ExecuteAsync("delete from 'Speaker' where confId = '" + confId + "'");
        }

        private async Task ClearFloors(string confId)
        {
            await sqlConnection.ExecuteAsync("delete from 'Floor' where confId = '" + confId + "'");
        }

        private async Task ClearTracks(string confId)
        {
            await sqlConnection.ExecuteAsync("delete from 'Track' where confId = '" + confId + "'");
        }

        private async Task ClearETag()
        {
            await sqlConnection.ExecuteAsync("delete from 'ETag'");
        }

        private void DBConnection()
        {
            if (sqlConnection != null) return;

            var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "db.sqlite");

            SQLiteConnectionString connString = new SQLiteConnectionString(path.ToString(), false);
            SQLiteConnectionWithLock connLock = new SQLiteConnectionWithLock(new SQLite.Net.Platform.WinRT.SQLitePlatformWinRT(), connString);

            sqlConnection = new SQLiteAsyncConnection(() => connLock);
        }

        public async Task<List<Track>> GetTracks()
        {
            string confId = currentConferenceId();
            return await sqlConnection.Table<Track>()
                .Where(t => t.confId.Equals(confId))
                .OrderBy(t => t.title)
                .ToListAsync();
        }

        public async Task<List<Speaker>> GetSpeakers()
        {
            string confId = currentConferenceId();
            return await sqlConnection.Table<Speaker>()
                .Where(s => s.confId.Equals(confId))
                .OrderBy(s => s.firstName)
                .ToListAsync();
        }

        public async Task<List<Speaker>> GetSpeakersBySearchCriteria(string searchString)
        {
            string confId = currentConferenceId();
            return await sqlConnection.Table<Speaker>()
                .Where(t => t.confId.Equals(confId) &&
                    (
                    (t.firstName != null && t.firstName.Contains(searchString)) ||
                    (t.lastName != null && t.lastName.Contains(searchString)) ||
                    (t.company != null && t.company.Contains(searchString))
                    )
                 )
                .OrderBy(s => s.firstName)
                .ToListAsync();
        }

        public async Task<Speaker> GetSpeaker(string uuid)
        {
            Tuple<Speaker, bool> speakerTuple = await Service.GetSpeaker(uuid);
            return speakerTuple.Item1;
        }

        public async Task<List<Model.Floor>> GetFloors(string target)
        {
            string confId = currentConferenceId();
            List<Model.Floor> floors = await sqlConnection.Table<Model.Floor>().Where(t => t.confId.Equals(confId) && t.target.Equals(target)).ToListAsync();
            if (floors.Count == 0)
            {
                return await sqlConnection.Table<Model.Floor>().Where(t => t.confId.Equals(confId)).ToListAsync();
            }
            return floors;
        }

        public async Task<Conference> GetCurrentConference()
        {
            string confId = currentConferenceId();
            return await sqlConnection.Table<Conference>().Where(t => t.id.Equals(confId)).FirstOrDefaultAsync();
        }

        public async Task<List<Event>> GetEventsByTrack(string trackId)
        {
            string confId = currentConferenceId();
            var eventList = await sqlConnection.Table<Event>().Where(t => t.confId.Equals(confId) && t.trackId.Equals(trackId)).ToListAsync();

            await AttachSpeakers(eventList);

            return eventList;
        }

        public async Task<List<Event>> GetEventsByDay(string day)
        {
            string confId = currentConferenceId();
            var eventList = await sqlConnection.Table<Event>()
                .Where(t => t.confId.Equals(confId) && t.day.Equals(day))
                .OrderBy(e => e.fullTime)
                .ToListAsync();

            await AttachSpeakers(eventList);

            return eventList;
        }

        public async Task<Event> GetEventsById(string id)
        {
            string confId = currentConferenceId();
            var e = await sqlConnection.Table<Event>().Where(t => t.confId.Equals(confId) && t.id.Equals(id)).FirstOrDefaultAsync();

            if (e != null)
            {
                await AttachSpeakers(e);
            }

            return e;
        }

        public async Task<List<Event>> GetEventsBySearchCriteria(string searchString)
        {
            string confId = currentConferenceId();
            var eventList = await sqlConnection.Table<Event>()
                .Where(t => t.confId.Equals(confId) && t.type.Equals(EventType.TALK) &&
                    (t.title.Contains(searchString) ||
                    t.summary.Contains(searchString) ||
                    t.speakerNames.Contains(searchString) ||
                    t.trackName.Contains(searchString)))
                .OrderByDescending(e => e.Starred)
                .OrderBy(e => e.title)
                .ToListAsync();

            await AttachSpeakers(eventList);

            return eventList;
        }

        public async Task<List<Event>> GetStarredEvents()
        {
            string confId = currentConferenceId();
            return await sqlConnection.Table<Event>().Where(e => e.confId.Equals(confId) && e.Starred).ToListAsync();
        }

        private string currentConferenceId()
        {
            return (string)settings.Values[Settings.CONFERENCE_ID];
        }

        public async Task<Event> UpdateEvent(Event e)
        {
            await sqlConnection.UpdateAsync(e);
            return e;
        }

        public async Task<Note> SaveOrUpdateNote(Note note)
        {
            string confId = currentConferenceId();
            note.confId = confId;
            note.id = confId + note.talkId;
            await sqlConnection.InsertOrReplaceAsync(note);
            return note;
        }

        public async Task<Note> GetNote(string talkId)
        {
            string confId = currentConferenceId();
            Note note;
            List<Note> notes = await sqlConnection.Table<Note>().Where(n => n.talkId.Equals(talkId) && n.confId.Equals(confId)).ToListAsync();
            if (notes.Count == 0)
            {
                note = new Note();
                note.talkId = talkId;
            }
            else
            {
                note = notes.First();
            }
            return note;
        }

        public async Task<Vote> SaveOrUpdateVote(Vote vote)
        {
            string confId = currentConferenceId();
            vote.confId = confId;
            vote.id = confId + vote.talkId;
            await sqlConnection.InsertOrReplaceAsync(vote);
            return vote;
        }

        public async Task<Vote> GetVote(string talkId)
        {
            string confId = currentConferenceId();
            Vote vote;
            List<Vote> votes = await sqlConnection.Table<Vote>().Where(n => n.talkId.Equals(talkId) && n.confId.Equals(confId)).ToListAsync();
            if (votes.Count == 0)
            {
                vote = new Vote();
                vote.talkId = talkId;
            }
            else
            {
                vote = votes.First();
            }
            return vote;
        }

        private async Task AttachSpeakers(List<Event> eventList)
        {
            List<Speaker> speakerList = await GetSpeakers();
            var speakerMap = speakerList.GroupBy(x => x.uuid)
                                .ToDictionary(x => x.Key, x => x.First());
            foreach (Event e in eventList)
            {
                if (string.IsNullOrEmpty(e.speakerId))
                {
                    continue;
                }
                string[] speakerIds = e.speakerId.Split(',');
                foreach (string id in speakerIds)
                {
                    Speaker s;
                    if (speakerMap.TryGetValue(id, out s))
                    {
                        e.SpeakerList.Add(s);
                    }
                }
            }
        }

        private async Task AttachSpeakers(Event e)
        {
            List<Speaker> speakerList = await GetSpeakers();
            var speakerMap = speakerList.GroupBy(x => x.uuid)
                                .ToDictionary(x => x.Key, x => x.First());
            if (string.IsNullOrEmpty(e.speakerId))
            {
                return;
            }
            string[] speakerIds = e.speakerId.Split(',');
            foreach (string id in speakerIds)
            {
                Speaker s;
                if (speakerMap.TryGetValue(id, out s))
                {
                    e.SpeakerList.Add(s);
                }
            }
        }
    }
}
