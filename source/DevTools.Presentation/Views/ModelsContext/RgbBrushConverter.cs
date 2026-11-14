using DevTools.ModelsContext.Model;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DevTools.Presentation.Views.ModelsContext;

public sealed class RgbBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RgbColor rgb)
        {
            return null;
        }

        var brush = new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
