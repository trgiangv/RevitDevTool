using System.Globalization;
using System.Windows.Data;

namespace RevitDevTool.View.Converters;

/// <summary>
/// Converts an integer value to a Visibility value (0 = Collapsed, non-zero = Visible)
/// </summary>
[ValueConversion(typeof(int), typeof(System.Windows.Visibility))]
public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
