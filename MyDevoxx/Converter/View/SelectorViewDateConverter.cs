using MyDevoxx.Model;
using System;
using System.Diagnostics;
using System.Globalization;
using Windows.UI.Xaml.Data;

namespace MyDevoxx.Converter.View
{
    public class SelectorViewDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
            {
                return "";
            }

            Conference conference = (Conference)value;
            DateTime fromDate, toDate;
            Debug.WriteLine(CultureInfo.CurrentCulture);
            bool isSuccessfulFrom = DateTime.TryParseExact(conference.fromDate, "yyyy-MM-dTHH:mm:ss.000Z", CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal, out fromDate);
            bool isSuccessfulTo = DateTime.TryParseExact(conference.toDate, "yyyy-MM-dTHH:mm:ss.000Z", CultureInfo.CurrentCulture, DateTimeStyles.AdjustToUniversal, out toDate);

            if (fromDate != null && toDate != null && isSuccessfulFrom && isSuccessfulTo)
            {
                return "from " + fromDate.Day + " to " + toDate.ToString("dd/MM/yyyy");
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
