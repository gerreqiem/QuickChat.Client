using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace QuickChat.Client.Converters
{
    public class BooleanToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isSentByMe && isSentByMe)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0088cc"));
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B0BEC5"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
