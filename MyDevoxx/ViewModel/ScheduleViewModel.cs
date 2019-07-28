using MyDevoxx.Model;
using MyDevoxx.Services;
using MyDevoxx.Utils;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Practices.ServiceLocation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;
using System.Globalization;

namespace MyDevoxx.ViewModel
{
    public class ScheduleViewModel : ViewModelBase
    {
        private ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
        private static string DAY_FILTER_SETTINGS = "DayFilterScheduleView";
        private static string TRACK_FILTER_SETTINGS = "TrackFilterScheduleView";
        private static string SEARCH_SETTINGS = "SearchStringScheduleView";
        private static string JUST_FAVS_SETTINGS = "JustFavorites";

        private IDataService Service;

        private bool isLoaded;
        private bool refreshRequested;

        private string[] DayNames = { "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" };
        private string[] DayPivotNames = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        private string SearchString = default(string);

        public ObservableCollection<SchedulePivotItem> _pivotItems = new ObservableCollection<SchedulePivotItem>();
        public ObservableCollection<SchedulePivotItem> PivotItems
        {
            get { return _pivotItems; }
            set
            {
                if (Set("PivotItems", ref _pivotItems, value))
                {
                    RaisePropertyChanged(() => PivotItems);
                }
            }
        }

        // Lists for the filter overlay
        private ObservableCollection<FilterItem> _trackList = new ObservableCollection<FilterItem>();
        public ObservableCollection<FilterItem> TrackList
        {
            get { return _trackList; }
        }

        private ObservableCollection<FilterItem> _dayList = new ObservableCollection<FilterItem>();
        public ObservableCollection<FilterItem> DayList
        {
            get { return _dayList; }
        }
        // Lists for the filter action
        private List<String> _trackFilter = new List<string>();
        public List<String> TrackFilter
        {
            get { return _trackFilter; }
        }

        private List<String> _dayFilter = new List<string>();
        public List<String> DayFilter
        {
            get { return _dayFilter; }
        }
        public int FilterCount
        {
            get { return DayFilter.Count + TrackFilter.Count; }
        }
        private bool _justFavorites = false;
        public bool JustFavorites
        {
            get { return _justFavorites; }
            set
            {
                settings.Values[JUST_FAVS_SETTINGS] = value;
                if (value)
                {
                    Messenger.Default.Send<MessageType>(MessageType.SHOW_FAVS);
                }
                else
                {
                    Messenger.Default.Send<MessageType>(MessageType.HIDE_FAVS);
                }
                if (Set("JustFavorites", ref _justFavorites, value))
                {
                    RaisePropertyChanged(() => JustFavorites);
                }
            }
        }

        private bool _showSpeakerImages = true;
        public bool ShowSpeakerImages
        {
            get { return _showSpeakerImages; }
            set
            {
                if (Set("ShowSpeakerImages", ref _showSpeakerImages, value))
                {
                    RaisePropertyChanged(() => ShowSpeakerImages);
                }
            }
        }

        // UI Commands
        private RelayCommand<FilterItem> _trackFilterCommand;
        public RelayCommand<FilterItem> TrackFilterCommand
        {
            get { return _trackFilterCommand; }
            private set { _trackFilterCommand = value; }
        }

        private RelayCommand<FilterItem> _dayFilterCommand;
        public RelayCommand<FilterItem> DayFilterCommand
        {
            get { return _dayFilterCommand; }
            private set { _dayFilterCommand = value; }
        }

        private RelayCommand<String> _searchCommand;
        public RelayCommand<String> SearchCommand
        {
            get { return _searchCommand; }
            private set { _searchCommand = value; }
        }

        public ScheduleViewModel()
        {
            Service = ServiceLocator.Current.GetInstance<IDataService>();
            Messenger.Default.Register<Event>(this, UpdateEvents);
            Messenger.Default.Register<MessageType>(this, ApplyFilter);
            Messenger.Default.Register<MessageType>(this, ClearFilter);
            Messenger.Default.Register<MessageType>(this, RequestRefresh);

            TrackFilterCommand = new RelayCommand<FilterItem>(TrackFilterTapped);
            DayFilterCommand = new RelayCommand<FilterItem>(DayFilterTapped);
            SearchCommand = new RelayCommand<string>(Search);

            // reload filter settings
            var dayFilterSettings = settings.Values[DAY_FILTER_SETTINGS];
            if (dayFilterSettings != null)
            {
                foreach (string s in (string[])dayFilterSettings)
                {
                    DayFilter.Add(s);
                }
            }
            var trackFilterSettings = settings.Values[TRACK_FILTER_SETTINGS];
            if (trackFilterSettings != null)
            {
                foreach (string s in (string[])trackFilterSettings)
                {
                    TrackFilter.Add(s);
                }
            }
            var searchSettings = settings.Values[SEARCH_SETTINGS];
            if (searchSettings != null)
            {
                SearchString = (string)searchSettings;
            }
            var justFavsSettings = settings.Values[JUST_FAVS_SETTINGS];
            if (justFavsSettings != null)
            {
                JustFavorites = (bool)justFavsSettings;
            }
        }

        public async void LoadData()
        {
            if (settings.Values[Settings.SHOW_SPEAKER_IMG] != null)
            {
                ShowSpeakerImages = (bool)settings.Values[Settings.SHOW_SPEAKER_IMG];
            }

            if (isLoaded)
            {
                return;
            }
            isLoaded = true;

            if (refreshRequested)
            {
                refreshRequested = false;
                PivotItems.Clear();
            }

            Conference conference = await Service.GetCurrentConference();
            DateTime fromDate = DateTime.ParseExact(conference.fromDate, "yyyy-MM-dTHH:mm:ss.000Z", CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal);
            DateTime toDate = DateTime.ParseExact(conference.toDate, "yyyy-MM-dTHH:mm:ss.000Z", CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal);
            int dayCnt = (int)(toDate - fromDate).TotalDays;
            int dayStart = (int)fromDate.DayOfWeek;

            if (String.IsNullOrEmpty(SearchString))
            {
                for (int i = 0; i <= dayCnt; i++)
                {
                    int day = fromDate.AddDays(i).Day;
                    if (DayFilter.Contains(DayPivotNames[i + dayStart] + " " + day))
                    {
                        continue;
                    }
                    IEnumerable<SlotGroup> groups = await LoadSlotGroup(DayNames[i + dayStart]);

                    PivotItems.Add(new SchedulePivotItem(DayPivotNames[i + dayStart] + " " + day, DayNames[i + dayStart], groups));
                }
            }
            else
            {
                List<SlotGroup> groups = new List<SlotGroup>();
                groups.Add(await LoadSearchResult());
                PivotItems.Add(new SchedulePivotItem("Result", "", groups));
            }

            if (TrackList.Count == 0)
            {
                List<Track> tracks = await Service.GetTracks();
                foreach (Track t in tracks)
                {
                    TrackList.Add(new FilterItem(t.title, t.id, !TrackFilter.Contains(t.id)));
                }
            }
            if (DayList.Count == 0)
            {
                for (int i = 0; i <= dayCnt; i++)
                {
                    int day = fromDate.AddDays(i).Day;
                    string s = DayPivotNames[i + dayStart] + " " + day;
                    DayList.Add(new FilterItem(s, s, !DayFilter.Contains(s)));
                }
            }
        }

        private async Task<IEnumerable<SlotGroup>> LoadSlotGroup(string day)
        {
            List<Event> result = await Service.GetEventsByDay(day);
            IEnumerable<SlotGroup> groups;
            if (_justFavorites)
            {
                groups = from item in result
                         where !TrackFilter.Contains(item.trackId)
                         && item.Starred == true
                         group item by item.fullTime into slotGroup
                         select new SlotGroup(slotGroup, false)
                         {
                             GroupName = slotGroup.Key
                         };
            }
            else
            {
                groups = from item in result
                         where !TrackFilter.Contains(item.trackId)
                         group item by item.fullTime into slotGroup
                         select new SlotGroup(slotGroup, false)
                         {
                             GroupName = slotGroup.Key
                         };
            }
            return groups;
        }

        private async Task<SlotGroup> LoadSearchResult()
        {
            List<Event> result = await Service.GetEventsBySearchCriteria(SearchString);
            return new SlotGroup(result, true)
            { GroupName = "Found: " + result.Count };
        }

        public override void Cleanup()
        {
            isLoaded = false;
            PivotItems.Clear();

            TrackList.Clear();
            DayList.Clear();
            TrackFilter.Clear();
            DayFilter.Clear();

            settings.Values[DAY_FILTER_SETTINGS] = null;
            settings.Values[TRACK_FILTER_SETTINGS] = null;
            settings.Values[SEARCH_SETTINGS] = null;
        }

        private async void UpdateEvents(Event e)
        {
            foreach (SchedulePivotItem day in PivotItems)
            {
                if (day.day.Equals(e.day))
                {
                    day.slotGroups.Clear();
                    IEnumerable<SlotGroup> groups = await LoadSlotGroup(e.day);
                    foreach (SlotGroup g in groups)
                    {
                        day.slotGroups.Add(g);
                    }
                    break;
                }
            }
        }

        private void ApplyFilter(MessageType type)
        {
            if (type.Equals(MessageType.REFRESH_SCHEDULE))
            {
                isLoaded = false;
                PivotItems.Clear();

                if (DayFilter.Count > 0)
                {
                    settings.Values[DAY_FILTER_SETTINGS] = DayFilter.ToArray();
                }
                else
                {
                    settings.Values[DAY_FILTER_SETTINGS] = null;
                }
                if (TrackFilter.Count > 0)
                {
                    settings.Values[TRACK_FILTER_SETTINGS] = TrackFilter.ToArray();
                }
                else
                {
                    settings.Values[TRACK_FILTER_SETTINGS] = null;
                }

                LoadData();
            }
        }

        private void ClearFilter(MessageType type)
        {
            if (type.Equals(MessageType.CLEAR_FILTER_SCHEDULE))
            {
                isLoaded = false;
                PivotItems.Clear();
                TrackFilter.Clear();
                DayFilter.Clear();
                TrackList.Clear();
                DayList.Clear();
                JustFavorites = false;

                settings.Values[DAY_FILTER_SETTINGS] = null;
                settings.Values[TRACK_FILTER_SETTINGS] = null;
                settings.Values[JUST_FAVS_SETTINGS] = null;

                LoadData();
            }
        }

        public void TrackFilterTapped(FilterItem item)
        {
            if (TrackFilter.Contains(item.FilterValue))
            {
                TrackFilter.Remove(item.FilterValue);
                item.IsChecked = true;
            }
            else
            {
                TrackFilter.Add(item.FilterValue);
                item.IsChecked = false;
            }
        }


        public void DayFilterTapped(FilterItem item)
        {
            if (DayFilter.Contains(item.FilterValue))
            {
                DayFilter.Remove(item.FilterValue);
                item.IsChecked = true;
            }
            else
            {
                DayFilter.Add(item.FilterValue);
                item.IsChecked = false;
            }
        }

        private void Search(string searchString)
        {
            SearchString = searchString;
            isLoaded = false;
            PivotItems.Clear();
            if (String.IsNullOrEmpty(searchString))
            {
                settings.Values[SEARCH_SETTINGS] = null;
            }
            else
            {
                settings.Values[SEARCH_SETTINGS] = searchString;
            }

            LoadData();
            Messenger.Default.Send<MessageType>(MessageType.SEARCH_COMPLETED);
        }

        private void RequestRefresh(MessageType messageType)
        {
            if (MessageType.REQUEST_REFRESH_SCHEDULE.Equals(messageType))
            {
                refreshRequested = true;
                isLoaded = false;
            }
        }
    }

    public class SlotGroup
    {
        public string GroupName { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        private ObservableCollection<Object> _eventLists = new ObservableCollection<Object>();
        public ObservableCollection<Object> EventLists
        {
            get
            {
                return _eventLists;
            }
            set
            {
                _eventLists = value;
            }
        }

        public SlotGroup(IEnumerable<Event> items, bool asPlainList)
        {
            if (asPlainList)
            {
                foreach (Event e in items)
                {
                    EventLists.Add(e);
                }
            }
            else
            {
                Event first = items.First();
                DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                StartDateTime = epoch.AddMilliseconds(first.fromTimeMillis);
                EndDateTime = epoch.AddMilliseconds(first.toTimeMillis);

                List<Event> unstarred = new List<Event>();
                foreach (Event e in items)
                {
                    if (e.type.Equals(EventType.BREAK))
                    {
                        EventLists.Add(e);
                    }
                    else if (e.Starred)
                    {
                        EventLists.Add(e);
                    }
                    else
                    {
                        unstarred.Add(e);
                    }
                }
                if (unstarred.Count > 0)
                {
                    EventLists.Add(unstarred);
                }
            }
        }
    }

    public class SchedulePivotItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string title { get; set; }
        private ObservableCollection<SlotGroup> _slotGroups = new ObservableCollection<SlotGroup>();
        public ObservableCollection<SlotGroup> slotGroups
        {
            get { return _slotGroups; }
            set
            {
                _slotGroups = value;
                NotifyPropertyChanged();
            }
        }
        public string day { get; set; }

        public SchedulePivotItem(string title, string day, IEnumerable<SlotGroup> slotGroups)
        {
            this.title = title;
            this.day = day;
            this.slotGroups = new ObservableCollection<SlotGroup>(slotGroups);
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
