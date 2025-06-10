using System;
using System.Windows.Data;

namespace QuickChat.Client.Converters
{
    public class BooleanToOnlineOfflineConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? "(Online)" : "(Offline)";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}