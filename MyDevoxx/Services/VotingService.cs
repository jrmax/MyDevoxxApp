﻿using MyDevoxx.Model;
using MyDevoxx.Services.RestModel.Voting;
using Microsoft.Practices.ServiceLocation;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using MyDevoxx.Utils;

namespace MyDevoxx.Services
{
    public class VotingService : IVotingService
    {
        private IRestService Service;
        private ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;

        private static string USERID = "userId";

        public VotingService()
        {
            Service = ServiceLocator.Current.GetInstance<IRestService>();
        }

        public async Task<VoteMessage> Vote(Vote vote)
        {
            string confId = (string)settings.Values[Settings.CONFERENCE_ID];
            string userId = (string)settings.Values[USERID + confId];

            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            VoteMessage message;
            if (string.IsNullOrWhiteSpace(vote.content) &&
                string.IsNullOrWhiteSpace(vote.delivery) &&
                string.IsNullOrWhiteSpace(vote.other))
            {
                VoteBasic voteBasic = new VoteBasic();
                voteBasic.talkId = vote.talkId;
                voteBasic.user = userId;
                voteBasic.rating = vote.rating;

                message = await Service.VoteTalk(voteBasic);
            }
            else {
                VoteReviews voteReviews = new VoteReviews();
                voteReviews.talkId = vote.talkId;
                voteReviews.user = userId;

                List<VoteDetail> details = new List<VoteDetail>();
                if (!string.IsNullOrEmpty(vote.content))
                {
                    details.Add(CreateVoteDetails(vote.rating, "Content", vote.content));
                }
                if (!string.IsNullOrEmpty(vote.delivery))
                {
                    details.Add(CreateVoteDetails(vote.rating, "Delivery", vote.delivery));
                }
                if (!string.IsNullOrEmpty(vote.other))
                {
                    details.Add(CreateVoteDetails(vote.rating, "Other", vote.other));
                }
                voteReviews.details = details;

                message = await Service.VoteTalk(voteReviews);
            }
            return message;
        }

        private VoteDetail CreateVoteDetails(int rating, string aspect, string review)
        {
            VoteDetail details = new VoteDetail();
            details.aspect = aspect;
            details.rating = rating;
            details.review = review;

            return details;
        }

    }
}
