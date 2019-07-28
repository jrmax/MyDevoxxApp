using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Views;
using Microsoft.Practices.ServiceLocation;
using MyDevoxx.Services;
using MyDevoxx.Views;

namespace MyDevoxx.ViewModel
{
    /// <summary>
    /// This class contains static references to all the view models in the
    /// application and provides an entry point for the bindings.
    /// </summary>
    public class ViewModelLocator
    {
        public const string ScheduleViewKey = "ScheduleView";
        public const string MapViewKey = "MapView";
        public const string TracksViewKey = "TracksView";
        public const string SpeakersViewKey = "SpeakersView";
        public const string ConferenceSelectorViewKey = "ConferenceSelectorView";
        public const string TalkDetailsViewKey = "TalkDetailsView";
        public const string SpeakerDetailsViewKey = "SpeakerDetailsView";
        public const string SettingsViewKey = "SettingsView";
        public const string AboutViewKey = "AboutView";
        public const string CreditsViewKey = "CreditsView";
        public const string RegisterViewKey = "RegisterView";
        public const string VotingResultViewKey = "VotingResultView";

        static ViewModelLocator()
        {
            ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);

            var nav = new NavigationService();
            SimpleIoc.Default.Register<INavigationService>(() => nav);
            nav.Configure(ViewModelLocator.ScheduleViewKey, typeof(ScheduleView));
            nav.Configure(ViewModelLocator.MapViewKey, typeof(MapView));
            nav.Configure(ViewModelLocator.TracksViewKey, typeof(TracksView));
            nav.Configure(ViewModelLocator.SpeakersViewKey, typeof(SpeakersView));
            nav.Configure(ViewModelLocator.ConferenceSelectorViewKey, typeof(ConferenceSelectorView));
            nav.Configure(ViewModelLocator.TalkDetailsViewKey, typeof(TalkDetailsView));
            nav.Configure(ViewModelLocator.SpeakerDetailsViewKey, typeof(SpeakerDetailsView));
            nav.Configure(ViewModelLocator.SettingsViewKey, typeof(SettingsView));
            nav.Configure(ViewModelLocator.CreditsViewKey, typeof(CreditsView));
            nav.Configure(ViewModelLocator.AboutViewKey, typeof(AboutView));
            nav.Configure(ViewModelLocator.RegisterViewKey, typeof(RegisterView));
            nav.Configure(ViewModelLocator.VotingResultViewKey, typeof(VotingResultView));

            SimpleIoc.Default.Register<IDialogService, DialogService>();
            SimpleIoc.Default.Register<IRestService, RestService>();
            SimpleIoc.Default.Register<IDataService, DataService>();
            SimpleIoc.Default.Register<ITwitterService, TwitterService>();
            SimpleIoc.Default.Register<IVotingService, VotingService>();
            SimpleIoc.Default.Register<INotificationService, NotificationService>();

            SimpleIoc.Default.Register<MapViewModel>();
            SimpleIoc.Default.Register<ScheduleViewModel>();
            SimpleIoc.Default.Register<SpeakersViewModel>();
            SimpleIoc.Default.Register<SpeakerDetailsViewModel>();
            SimpleIoc.Default.Register<TracksViewModel>();
            SimpleIoc.Default.Register<TalkDetailsViewModel>();
            SimpleIoc.Default.Register<SettingsViewModel>();
            SimpleIoc.Default.Register<AboutViewModel>();
            SimpleIoc.Default.Register<VotingResultViewModel>();
            SimpleIoc.Default.Register<ConferenceSelectorViewModel>(true);
        }

        public MapViewModel Map => ServiceLocator.Current.GetInstance<MapViewModel>();
        public ScheduleViewModel Schedule => ServiceLocator.Current.GetInstance<ScheduleViewModel>();
        public SpeakersViewModel Speakers => ServiceLocator.Current.GetInstance<SpeakersViewModel>();
        public TracksViewModel Tracks => ServiceLocator.Current.GetInstance<TracksViewModel>();
        public ConferenceSelectorViewModel ConferenceSelectorViewModel => ServiceLocator.Current.GetInstance<ConferenceSelectorViewModel>();
        public TalkDetailsViewModel TalkDetails => ServiceLocator.Current.GetInstance<TalkDetailsViewModel>();
        public SpeakerDetailsViewModel SpeakerDetails => ServiceLocator.Current.GetInstance<SpeakerDetailsViewModel>();
        public AboutViewModel About => ServiceLocator.Current.GetInstance<AboutViewModel>();
        public SettingsViewModel Settings => ServiceLocator.Current.GetInstance<SettingsViewModel>();
        public VotingResultViewModel VotingResult => ServiceLocator.Current.GetInstance<VotingResultViewModel>();

        public static void Cleanup()
        {
            ServiceLocator.Current.GetInstance<MapViewModel>().Cleanup();
            ServiceLocator.Current.GetInstance<ScheduleViewModel>().Cleanup();
            ServiceLocator.Current.GetInstance<SpeakersViewModel>().Cleanup();
            ServiceLocator.Current.GetInstance<TracksViewModel>().Cleanup();
        }
    }
}