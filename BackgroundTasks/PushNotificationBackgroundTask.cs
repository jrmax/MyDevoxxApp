using System;
using Windows.ApplicationModel.Background;
using Windows.Networking.PushNotifications;
using Windows.Storage;

namespace BackgroundTasks
{
    public sealed class PushNotificationBackgroundTask : IBackgroundTask
    {
        private ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
        private const String FORCE_UPDATE = "ForceUpdate";

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            if (taskInstance != null
                && taskInstance.TriggerDetails != null
                && taskInstance.TriggerDetails.GetType().Equals(typeof(RawNotification)))
            {
                settings.Values[FORCE_UPDATE] = DateTime.Now.ToString();
            }

            taskInstance.GetDeferral().Complete();
        }
    }
}
