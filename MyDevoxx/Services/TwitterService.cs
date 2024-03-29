﻿using MyDevoxx.Model;
using MyDevoxx.Utils;
using System;
using System.Net;
using Windows.Storage;
using Windows.System;

namespace MyDevoxx.Services
{
    public class TwitterService : ITwitterService
    {
        private static string twitterBaseUri = "https://twitter.com";
        private static string twitterTweetUri = twitterBaseUri + "/intent/tweet?text=";
        private static string twitterSearchUri = twitterBaseUri + "/search?q=";


        private ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;

        public async void sendSpeakerTweet(Speaker s)
        {
            await Launcher.LaunchUriAsync(new Uri(twitterTweetUri + s.twitter));
        }

        public async void sendTalkTweet(Event e)
        {
            string talkURL = (string)settings.Values[Settings.TALK_URL];
            string msg;
            if (string.IsNullOrEmpty(talkURL))
            {
                msg = WebUtility.UrlEncode(e.title);
            }
            else
            {
                msg = WebUtility.UrlEncode(e.title + " " + talkURL + e.id);
            }
            await Launcher.LaunchUriAsync(new Uri(twitterTweetUri + msg));
        }

        public async void goToHashTag(string hashTag)
        {
            await Launcher.LaunchUriAsync(new Uri(twitterSearchUri + WebUtility.UrlEncode(hashTag)));
        }
    }
}
