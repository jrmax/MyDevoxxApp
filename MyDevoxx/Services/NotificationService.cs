using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Practices.ServiceLocation;
using MyDevoxx.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Networking.PushNotifications;
using Windows.Storage;

namespace MyDevoxx.Services
{
    class NotificationService : INotificationService
    {
        private const string TASK_NAME = "PushNotifyTask";
        private const string TASK_ENTRYPOINT = "BackgroundTasks.PushNotificationBackgroundTask";
        private const string ACCESS_KEY_ID = "ACCESS_KEY";
        private const string SECRET_ACCESS_KEY = "GEHEIM!";

        private const string PLATFORM_APPLICATION_ARN = "arn:aws:sns:eu-west-1:390215441069:app/WNS/MyDevoxxWindows";
        private const string TOPIC_ARN_PREFIX = "arn:aws:sns:eu-west-1:390215441069:";
        private const string TOPIC_GLOBAL = "cfp_schedule_updates";

        private ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
        private const string ENDPOINT_ARN = "EndpointArn";
        private AmazonSimpleNotificationServiceClient client;

        private IDataService dataService;
        private bool isServiceEnabled = false;

        public NotificationService()
        {
            if (!isServiceEnabled)
            {
                return;
            }
            client = new AmazonSimpleNotificationServiceClient(ACCESS_KEY_ID, SECRET_ACCESS_KEY, RegionEndpoint.EUWest1);
            dataService = ServiceLocator.Current.GetInstance<IDataService>();
        }

        public async void TryRegisterAndSubscribeKnownTopics()
        {
            if (!isServiceEnabled)
            {
                return;
            }

            bool pushEnabled;
            if (settings.Values[Settings.PUSH_UPDATE_ENABLED] == null
                || !Boolean.TryParse((string)settings.Values[Settings.PUSH_UPDATE_ENABLED], out pushEnabled)
                || !pushEnabled)
            {
                Unregister();
            }
            else
            {
                if (await TryRegister())
                {
                    SubscribeTobic(TOPIC_GLOBAL);
                }
            }
        }

        public async void Unregister()
        {
            try
            {
                PushNotificationChannel channel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
                channel.PushNotificationReceived -= OnPushNotification;
                UnregisterBackgroundTask();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Unregister notifications: " + ex.Message);
                Debug.WriteLine("Unregister notifications: " + ex.StackTrace);
            }
        }

        public async Task<bool> TryRegister()
        {
            try
            {
                //retrieve the latest token from the mobile OS
                PushNotificationChannel channel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
                Debug.WriteLine("Channel: " + channel.Uri);
                channel.PushNotificationReceived += OnPushNotification;
                RegisterBackgroundTask();

                //create endpoint
                string endpointArn = await CreatePlatformEndpoint(channel);
                settings.Values[ENDPOINT_ARN] = endpointArn;

                //call GetEndpointAttributes on the endpoint arn
                var request = new GetEndpointAttributesRequest();
                request.EndpointArn = endpointArn;
                var response = await client.GetEndpointAttributesAsync(request);
                Dictionary<string, string> attributes = response.Attributes;

                // check if endpoint attributes are up to date
                if (!attributes["Token"].Equals(channel.Uri) || !attributes["Enabled"].Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    SetEndpointAttributesRequest setAttributeRequest = new SetEndpointAttributesRequest();
                    setAttributeRequest.EndpointArn = endpointArn;
                    setAttributeRequest.Attributes.Add("Token", channel.Uri);
                    setAttributeRequest.Attributes.Add("Enabled", "true");
                    await client.SetEndpointAttributesAsync(setAttributeRequest);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Register notifications");
                Debug.WriteLine(ex.Message);
                Debug.WriteLine(ex.StackTrace);
                return false;
            }

            return true;
        }

        private async Task<string> CreatePlatformEndpoint(PushNotificationChannel channel)
        {
            var request = new CreatePlatformEndpointRequest();
            request.PlatformApplicationArn = PLATFORM_APPLICATION_ARN;
            request.Token = channel.Uri;

            CreatePlatformEndpointResponse response = await client.CreatePlatformEndpointAsync(request);

            return response.EndpointArn;
        }

        public async void SubscribeTobic(string topic)
        {
            if (settings.Values[ENDPOINT_ARN] == null)
            {
                if (await TryRegister())
                {
                    return;
                }
            }
            try
            {
                string endpoint = (string)settings.Values[ENDPOINT_ARN];

                SubscribeRequest subscribeRequest = new SubscribeRequest();
                subscribeRequest.TopicArn = TOPIC_ARN_PREFIX + topic;
                subscribeRequest.Protocol = "application";
                subscribeRequest.Endpoint = endpoint;
                SubscribeResponse response = await client.SubscribeAsync(subscribeRequest);
            }

            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private async void RegisterBackgroundTask()
        {
            var backgroundAccessStatus = await BackgroundExecutionManager.RequestAccessAsync();
            if (backgroundAccessStatus == BackgroundAccessStatus.AllowedMayUseActiveRealTimeConnectivity ||
                backgroundAccessStatus == BackgroundAccessStatus.AllowedWithAlwaysOnRealTimeConnectivity)
            {
                foreach (var task in BackgroundTaskRegistration.AllTasks)
                {
                    if (task.Value.Name == TASK_NAME)
                    {
                        task.Value.Unregister(true);
                    }
                }

                BackgroundTaskBuilder taskBuilder = new BackgroundTaskBuilder();
                taskBuilder.Name = TASK_NAME;
                taskBuilder.TaskEntryPoint = TASK_ENTRYPOINT;
                taskBuilder.SetTrigger(new PushNotificationTrigger());
                var registration = taskBuilder.Register();
            }
        }

        private async void UnregisterBackgroundTask()
        {
            var backgroundAccessStatus = await BackgroundExecutionManager.RequestAccessAsync();
            if (backgroundAccessStatus == BackgroundAccessStatus.AllowedMayUseActiveRealTimeConnectivity ||
                backgroundAccessStatus == BackgroundAccessStatus.AllowedWithAlwaysOnRealTimeConnectivity)
            {
                foreach (var task in BackgroundTaskRegistration.AllTasks)
                {
                    if (task.Value.Name == TASK_NAME)
                    {
                        task.Value.Unregister(true);
                    }
                }
            }
        }

        private void OnPushNotification(PushNotificationChannel sender, PushNotificationReceivedEventArgs e)
        {
            String notificationContent = String.Empty;
            switch (e.NotificationType)
            {
                case PushNotificationType.Badge:
                    Debug.WriteLine("Badge");
                    notificationContent = e.BadgeNotification.Content.GetXml();
                    break;

                case PushNotificationType.Tile:
                    Debug.WriteLine("Tile");
                    notificationContent = e.TileNotification.Content.GetXml();
                    break;

                case PushNotificationType.Toast:
                    Debug.WriteLine("Toast");
                    notificationContent = e.ToastNotification.Content.GetXml();
                    break;

                case PushNotificationType.Raw:
                    Debug.WriteLine("Raw");
                    notificationContent = e.RawNotification.Content;
                    settings.Values[Settings.FORCE_UPDATE] = DateTime.Now.ToString();

                    dataService.CheckUpdate();

                    e.Cancel = true;
                    break;
            }
            Debug.WriteLine(notificationContent);
        }
    }
}
