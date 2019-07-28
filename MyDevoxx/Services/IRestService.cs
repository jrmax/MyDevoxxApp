using MyDevoxx.Services.RestModel.Voting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyDevoxx.Services
{
    public interface IRestService
    {
        Task<Tuple<List<Model.Event>, bool>> GetEvents(string day);

        Task<Tuple<List<Model.Speaker>, bool>> GetSpeakers();

        Task<Tuple<Model.Speaker, bool>> GetSpeaker(string uuid);

        Task<Tuple<List<Model.Track>, bool>> GetTracks();

        Task<Tuple<List<Model.Conference>, bool>> GetConferences();

        Task<Tuple<List<Model.Floor>, bool>> GetFloors();

        Task<VoteMessage> VoteTalk(VoteBasic vote);

        Task<VoteMessage> VoteTalk(VoteReviews vote);

        Task<VoteResults> GetVoteResults(int limit, string day, string talkType, string track);

        Task<Categories> GetVoteCategories();

        Task LoadImage(List<string> httpUrls, string folder);
    }
}
