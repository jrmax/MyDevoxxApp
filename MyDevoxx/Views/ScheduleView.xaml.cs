using MyDevoxx.UserControls;
using MyDevoxx.Utils;
using MyDevoxx.ViewModel;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Views;
using Microsoft.Practices.ServiceLocation;
using System.Linq;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.Graphics.Display;
using System.Collections.Generic;
using MyDevoxx.Model;
using System.Diagnostics;

namespace MyDevoxx.Views
{
    public sealed partial class ScheduleView : Page
    {
        private ScheduleViewModel vm;
        private ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
        private static string SEARCH_SETTINGS = "SearchStringScheduleView";

        private Event changedEvent;

        public ScheduleView()
        {
            DisplayInformation.AutoRotationPreferences = DisplayOrientations.None;
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;

            vm = ((ScheduleViewModel)DataContext);

            Window.Current.SizeChanged += Current_SizeChanged;

            Messenger.Default.Register<Event>(this, EventChanged);
        }

        private void Current_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            var o = ApplicationView.GetForCurrentView().Orientation;
            if (o.Equals(ApplicationViewOrientation.Landscape))
            {
                VisualStateManager.GoToState(this, "Landscape", true);
            }
            else
            {
                VisualStateManager.GoToState(this, "Portrait", true);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            vm.LoadData();

            if (vm.FilterCount > 0)
            {
                ScheduleHeader.FilterIcon = Header.FILTER_ACTIVE;
            }
            else
            {
                ScheduleHeader.FilterIcon = Header.FILTER_INACTIVE;
            }

            var searchSettings = settings.Values[SEARCH_SETTINGS];
            if (searchSettings != null && (string)searchSettings != "")
            {
                ScheduleHeader.SearchString = (string)searchSettings;
                ScheduleHeader.ShowSearch();
            }

            ScheduleHeader.IndicatorFavorites = vm.JustFavorites;
        }

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Grid header = (Grid)sender;
            UIElement arrow = header.Children.Last();
            Grid parent = (Grid)header.Parent;
            UIElement list = parent.Children.Last();
            if (list.Visibility.Equals(Visibility.Visible))
            {
                list.Visibility = Visibility.Collapsed;
                ((CompositeTransform)arrow.RenderTransform).Rotation = 0;
            }
            else
            {
                list.Visibility = Visibility.Visible;
                ((CompositeTransform)arrow.RenderTransform).Rotation = 180;
            }
        }

        private void EventTalkControl_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (!typeof(EventTalkControl).Equals(sender.GetType()))
            {
                return;
            }
            var nav = ServiceLocator.Current.GetInstance<INavigationService>();
            nav.NavigateTo(ViewModelLocator.TalkDetailsViewKey, ((EventTalkControl)sender).DataContext);
        }

        private void ScheduleHeader_FilterTapped(object sender)
        {
            FilterGrid.Visibility = Visibility.Visible;
        }

        private void EventChanged(Event e)
        {
            this.changedEvent = e;
        }

        private void SetFocus()
        {
            ItemCollection items = SchedulePivot.Items;
            if (items.Count > 0)
            {
                SchedulePivotItem item = (SchedulePivotItem)items.FirstOrDefault(i => ((SchedulePivotItem)i).day.Equals(changedEvent.day));
                if (item == null)
                {
                    changedEvent = null;
                    return;
                }
                var container = SchedulePivot.ContainerFromItem(item);
                var children = AllChildren(container);
                var listView = (ListView)children.Find(c => c.Name == "ScheduleList");

                if (listView == null)
                {
                    changedEvent = null;
                    return;
                }
                var listViewItems = listView.Items;
                foreach (var listViewItem in listViewItems)
                {
                    if (listViewItem is List<Event>)
                    {
                        foreach (var e in listViewItem as List<Event>)
                        {
                            if (this.changedEvent.id.Equals(e.id))
                            {
                                listView.ScrollIntoView(e, ScrollIntoViewAlignment.Leading);
                                break;
                            }
                        }
                    }
                    else if (listViewItem is Event)
                    {
                        Event e = listViewItem as Event;
                        if (EventType.TALK.Equals(e.type) && this.changedEvent.id.Equals(e.id))
                        {
                            listView.ScrollIntoView(e, ScrollIntoViewAlignment.Leading);
                            break;
                        }

                    }
                }
            }
            this.changedEvent = null;
        }

        private List<Control> AllChildren(DependencyObject parent)
        {
            var result = new List<Control> { };
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Control)
                {
                    result.Add(child as Control);
                }
                result.AddRange(AllChildren(child));
            }
            return result;
        }

        public bool IsFilterVisile()
        {
            return FilterGrid.Visibility.Equals(Visibility.Visible);
        }

        public void CloseFilter()
        {
            this.Apply();
        }

        private void Apply_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.Apply();
        }

        private void Apply()
        {
            if (vm.FilterCount > 0)
            {
                ScheduleHeader.FilterIcon = Header.FILTER_ACTIVE;
            }
            else
            {
                ScheduleHeader.FilterIcon = Header.FILTER_INACTIVE;
            }
            FilterGrid.Visibility = Visibility.Collapsed;
            Messenger.Default.Send<MessageType>(MessageType.REFRESH_SCHEDULE);
        }

        private void Clear_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ScheduleHeader.FilterIcon = Header.FILTER_INACTIVE;
            FilterGrid.Visibility = Visibility.Collapsed;
            Messenger.Default.Send<MessageType>(MessageType.CLEAR_FILTER_SCHEDULE);
        }

        private void DayFilterGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (DayFilterList.Visibility.Equals(Visibility.Collapsed))
            {
                DayFilterList.Visibility = Visibility.Visible;
            }
            else
            {
                DayFilterList.Visibility = Visibility.Collapsed;
            }
        }

        private void TrackFilterGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (TrackFilterList.Visibility.Equals(Visibility.Collapsed))
            {
                TrackFilterList.Visibility = Visibility.Visible;
            }
            else
            {
                TrackFilterList.Visibility = Visibility.Collapsed;
            }
        }

        private void PivotItem_Loaded(object sender, RoutedEventArgs e)
        {
            if (changedEvent != null)
            {
                SetFocus();
            }
        }
    }
}
