using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace MyDevoxx.UserControls
{
    public sealed partial class EventTalkControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public static readonly DependencyProperty ShowSpeakerProperty =
            DependencyProperty.Register("ShowSpeaker", typeof(bool), typeof(EventTalkControl), new PropertyMetadata(true));
        public bool ShowSpeaker
        {
            get { return (bool)GetValue(ShowSpeakerProperty); }
            set
            {
                SetValue(ShowSpeakerProperty, value);
                NotifyPropertyChanged();
            }
        }

        public EventTalkControl()
        {
            this.InitializeComponent();
        }

        private void ImageBrush_ImageFailed(object sender, Windows.UI.Xaml.ExceptionRoutedEventArgs e)
        {
            ((ImageBrush)sender).ImageSource = new BitmapImage(new Uri(@"ms-appx:///Assets/speaker_placeholder.png"));
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
