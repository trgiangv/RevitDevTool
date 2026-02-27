using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace RevitDevTool.Desktop.Converters;

public class BoolToStatusBrushConverter : IValueConverter
{
    public static readonly BoolToStatusBrushConverter Instance = new();

    public IBrush TrueBrush { get; set; } = new SolidColorBrush(Color.Parse("#10B981"));
    public IBrush FalseBrush { get; set; } = new SolidColorBrush(Color.Parse("#EF4444"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? TrueBrush : FalseBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
