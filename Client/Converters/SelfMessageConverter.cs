using System;
using System.Globalization;
using System.Windows.Data;

namespace Client.Converters
{
    public class SelfMessageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string message = value as string;
            if (!string.IsNullOrEmpty(message) && (message.StartsWith("Me to") || message.StartsWith("Me in")))
            {
                return true;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}