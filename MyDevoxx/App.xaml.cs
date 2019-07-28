using MyDevoxx.Views;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Views;
using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using MyDevoxx.Utils;
using MyDevoxx.Services;
using Microsoft.Practices.ServiceLocation;

// The Blank Application template is documented at http://go.microsoft.com/fwlink/?LinkId=391641

namespace MyDevoxx
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public sealed partial class App : Application
    {
        private TransitionCollection transitions;
        private ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            Microsoft.ApplicationInsights.WindowsAppInitializer.InitializeAsync();

            this.InitializeComponent();
            this.Suspending += this.OnSuspending;
            this.Resuming += this.OnResuming;

            HardwareButtons.BackPressed += HardwareButtons_BackPressed;
        }

        private void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame != null &&
                rootFrame.Content is TalkDetailsView &&
                (rootFrame.Content as TalkDetailsView).IsVotingVisile())
            {
                (rootFrame.Content as TalkDetailsView).CloseVotingGrid();
                e.Handled = true;
            }
            else if (rootFrame != null && (
                      rootFrame.Content is TalkDetailsView ||
                      rootFrame.Content is SpeakerDetailsView ||
                      rootFrame.Content is SettingsView ||
                      rootFrame.Content is AboutView ||
                      rootFrame.Content is CreditsView ||
                      rootFrame.Content is RegisterView ||
                      rootFrame.Content is VotingResultView))
            {
                SimpleIoc.Default.GetInstance<INavigationService>().GoBack();
                e.Handled = true;
            }
            else if (rootFrame != null &&
                rootFrame.Content is ConferenceSelectorView &&
                settings.Values[Settings.CONFERENCE_ID] != null &&
                (bool)settings.Values[Settings.LOADED_SUCCESSFUL])
            {
                SimpleIoc.Default.GetInstance<INavigationService>().GoBack();
                e.Handled = true;
            }
            else if (rootFrame != null &&
                rootFrame.Content is ScheduleView &&
                (rootFrame.Content as ScheduleView).IsFilterVisile())
            {
                (rootFrame.Content as ScheduleView).CloseFilter();
                e.Handled = true;
            }
            else if (rootFrame != null &&
                rootFrame.Content is TracksView &&
                (rootFrame.Content as TracksView).IsFilterVisile())
            {
                (rootFrame.Content as TracksView).CloseFilter();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used when the application is launched to open a specific file, to display
        /// search results, and so forth.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif
            var dataService = ServiceLocator.Current.GetInstance<IDataService>();
            dataService.InitDB();

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                // TODO: change this value to a cache size that is appropriate for your application
                rootFrame.CacheSize = 1;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    // TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                // Removes the turnstile navigation for startup.
                if (rootFrame.ContentTransitions != null)
                {
                    this.transitions = new TransitionCollection();
                    foreach (var c in rootFrame.ContentTransitions)
                    {
                        this.transitions.Add(c);
                    }
                }

                rootFrame.ContentTransitions = null;
                rootFrame.Navigated += this.RootFrame_FirstNavigated;

                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter

                Type viewType;
                if (settings.Values[Settings.CONFERENCE_ID] != null
                    && settings.Values[Settings.LOADED_SUCCESSFUL] != null
                    && (bool)settings.Values[Settings.LOADED_SUCCESSFUL])
                {
                    viewType = typeof(ScheduleView);
                    dataService.CheckUpdate();
                }
                else
                {
                    viewType = typeof(ConferenceSelectorView);
                }

                if (!rootFrame.Navigate(viewType, e.Arguments))
                {
                    throw new Exception("Failed to create initial page");
                }

                INotificationService Service = ServiceLocator.Current.GetInstance<INotificationService>();
                Service.TryRegisterAndSubscribeKnownTopics();
            }

            // Ensure the current window is active
            Window.Current.Activate();
        }

        /// <summary>
        /// Restores the content transitions after the app has launched.
        /// </summary>
        /// <param name="sender">The object where the handler is attached.</param>
        /// <param name="e">Details about the navigation event.</param>
        private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e)
        {
            var rootFrame = sender as Frame;
            rootFrame.ContentTransitions = this.transitions ?? new TransitionCollection()
            {
                //new NavigationThemeTransition()
            };
            rootFrame.Navigated -= this.RootFrame_FirstNavigated;
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            // Save application state and stop any background activity
            deferral.Complete();
        }

        private void OnResuming(object sender, object e)
        {
            var dataService = ServiceLocator.Current.GetInstance<IDataService>();
            dataService.CheckUpdate();
        }
    }
}