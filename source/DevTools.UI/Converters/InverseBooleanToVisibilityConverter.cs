using System.Globalization;
using System.Windows.Data;
namespace DevTools.UI.Converters;

[ValueConversion(typeof(bool), typeof(System.Windows.Visibility))]
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }
        return System.Windows.Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is System.Windows.Visibility visibility)
        {
            return visibility != System.Windows.Visibility.Visible;
        }
        return false;
    }
}
