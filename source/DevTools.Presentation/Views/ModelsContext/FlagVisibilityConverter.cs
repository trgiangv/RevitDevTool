using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DevTools.Presentation.Views.ModelsContext;

public sealed class FlagVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Enum flags && parameter is string name && flags.HasFlag((Enum)Enum.Parse(flags.GetType(), name))
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
