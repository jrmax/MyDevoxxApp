using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Views;
using Microsoft.Practices.ServiceLocation;
using MyDevoxx.Services;
using MyDevoxx.Utils;
using Windows.Storage;

namespace MyDevoxx.ViewModel
{
    public class SettingsViewModel : ViewModelBase
    {
        private ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;

        private bool _updateInProgress = false;
        public bool UpdateInProgress
        {
            get { return _updateInProgress; }
            set
            {
                if (Set("UpdateInProgress", ref _updateInProgress, value))
                {
                    RaisePropertyChanged(() => UpdateInProgress);
                }
            }
        }
        private bool _removeFavoritsInProgress = false;
        public bool RemoveFavoritsInProgress
        {
            get { return _removeFavoritsInProgress; }
            set
            {
                if (Set("RemoveFavoritsInProgress", ref _removeFavoritsInProgress, value))
                {
                    RaisePropertyChanged(() => RemoveFavoritsInProgress);
                }
            }
        }

        public bool ShowSpeakerImages
        {
            get
            {
                if (settings.Values[Settings.SHOW_SPEAKER_IMG] == null)
                {
                    settings.Values[Settings.SHOW_SPEAKER_IMG] = true;
                }
                return (bool)settings.Values[Settings.SHOW_SPEAKER_IMG];
            }
            set
            {
                RaisePropertyChanged(() => ShowSpeakerImages);
                settings.Values[Settings.SHOW_SPEAKER_IMG] = value;
            }
        }

        private string _lastUpdate = "n/a";
        public string LastUpdate
        {
            get { return "last update: " + _lastUpdate; }
            set
            {
                if (Set("LastUpdate", ref _lastUpdate, value))
                {
                    RaisePropertyChanged(() => LastUpdate);
                }
            }
        }



        private RelayCommand _updateCommand;
        public RelayCommand UpdateCommand
        {
            get { return _updateCommand; }
            private set { _updateCommand = value; }
        }

        private RelayCommand _removeFavoritesCommand;
        public RelayCommand RemoveFavoritesCommand
        {
            get { return _removeFavoritesCommand; }
            private set { _removeFavoritesCommand = value; }
        }

        public SettingsViewModel()
        {
            UpdateCommand = new RelayCommand(ForceUpdate_Tapped);
            RemoveFavoritesCommand = new RelayCommand(RemoveFavorites_Tapped);
        }

        public void Load()
        {
            if(settings.Values[Settings.LAST_UPDATE + settings.Values[Settings.CONFERENCE_ID]] != null)
            {
                LastUpdate = (string)settings.Values[Settings.LAST_UPDATE + settings.Values[Settings.CONFERENCE_ID]];
            }
        }

        private async void RemoveFavorites_Tapped()
        {
            if (RemoveFavoritsInProgress) return;

            IDialogService dialogService = ServiceLocator.Current.GetInstance<IDialogService>();
            INavigationService navigationServive = ServiceLocator.Current.GetInstance<INavigationService>();
            await dialogService.ShowMessage("Do you really want to remove all favored talks?", "Confirmation", "Continue", "Discard", (confirmed) =>
            {
                if (confirmed)
                {
                    RemoveFavoritsInProgress = true;

                    IDataService dataService = ServiceLocator.Current.GetInstance<IDataService>();
                    dataService.removeFavorites((successful) => RemoveFavoritsInProgress = false);
                    ViewModelLocator.Cleanup();
                }
            });
        }

        private void ForceUpdate_Tapped()
        {
            if (UpdateInProgress) return;

            UpdateInProgress = true;
            settings.Values[Settings.FORCE_UPDATE] = "ManualUpdate";
            IDataService dataService = ServiceLocator.Current.GetInstance<IDataService>();
            dataService.CheckUpdate(OnUpdateFinished);
        }

        private void OnUpdateFinished(bool successful)
        {
            if (successful)
            {
                Messenger.Default.Send<MessageType>(MessageType.REFRESH_SPEAKERS);
                Messenger.Default.Send<MessageType>(MessageType.REQUEST_REFRESH_SCHEDULE);
                Messenger.Default.Send<MessageType>(MessageType.REFRESH_TRACKS);
                LastUpdate = (string)settings.Values[Settings.LAST_UPDATE + settings.Values[Settings.CONFERENCE_ID]];
            }
            UpdateInProgress = false;
        }
    }
}
