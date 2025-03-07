using System.Globalization;
using System.Windows.Data;

namespace Client.Converters;

public class SelfMessageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var message = value as string;
        if (!string.IsNullOrWhiteSpace(message))
        {
            message = message.TrimStart();
            if (message.StartsWith("Me to", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("Me in", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}