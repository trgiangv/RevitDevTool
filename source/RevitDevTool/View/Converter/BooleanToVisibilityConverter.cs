using System.Globalization;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;
namespace RevitDevTool.View.Converter;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var inverse = parameter != null && parameter.ToString() == "Inverse";
        if (value is bool boolValue)
        {
            return boolValue ^ inverse
                ? VisibilityBoxing(System.Windows.Visibility.Visible)
                : VisibilityBoxing(System.Windows.Visibility.Collapsed);
        }
        return Binding.DoNothing;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var inverse = parameter != null && parameter.ToString() == "Inverse";
        if (value is not System.Windows.Visibility visibility) return false;
        var result = visibility == System.Windows.Visibility.Visible;
        return BooleanBoxing(result ^ inverse);
    }

    private static object VisibilityBoxing(System.Windows.Visibility visibility) => visibility;
    private static object BooleanBoxing(bool value) => value;
}