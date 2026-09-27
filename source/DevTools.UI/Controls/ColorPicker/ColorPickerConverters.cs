using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
// ReSharper disable once CheckNamespace
namespace DevTools.UI.Controls;

public enum ColorChannel
{
    RMin,
    RMax,
    R,
    GMin,
    GMax,
    G,
    BMin,
    BMax,
    B,
    AMin,
    AMax,
    A
}

public enum HsvChannel
{
    SMin,
    SMax,
    S,
    VMin,
    VMax,
    V,
    SvMax
}

[ValueConversion(typeof(Color), typeof(SolidColorBrush))]
public sealed class ColorToSolidColorBrushConverter : IValueConverter
{
    public static readonly ColorToSolidColorBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is SolidColorBrush brush ? brush.Color : Colors.Transparent;
    }
}

public sealed class ColorToNameConverter : IMultiValueConverter
{
    public static readonly ColorToNameConverter Instance = new();

    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var color = values.OfType<Color?>().FirstOrDefault() ?? values.OfType<Color>().Cast<Color?>().FirstOrDefault();
        var names = values.OfType<Dictionary<Color, string>>().FirstOrDefault();
        var helper = values.OfType<ColorHelper>().FirstOrDefault() ?? ColorHelper.DefaultInstance;
        var useAlpha = values.OfType<bool>().FirstOrDefault();
        if (!values.OfType<bool>().Any())
        {
            useAlpha = true;
        }

        return helper.GetColorName(color, names, useAlpha);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class NumericValueConverter : IValueConverter
{
    public static readonly NumericValueConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is null ? 0d : System.Convert.ToDouble(value, culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var number = value is null ? 0d : System.Convert.ToDouble(value, culture);
        if (targetType == typeof(byte))
        {
            if (number < 0)
            {
                number = 0;
            }
            else if (number > 255)
            {
                number = 255;
            }

            return (byte)Math.Round(number);
        }

        return number;
    }
}

[ValueConversion(typeof(double), typeof(GridLength))]
public sealed class PercentageToGridLengthConverter : IValueConverter
{
    public static readonly PercentageToGridLengthConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double percent)
        {
            return Binding.DoNothing;
        }

        if (parameter as string == bool.TrueString)
        {
            percent = 1 - percent;
        }

        return new GridLength(percent, GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class HsvChannelBrushConverter : IValueConverter
{
    public static readonly HsvChannelBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (ChannelColor(value, parameter) is not { } color)
        {
            return Binding.DoNothing;
        }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    internal static Color? ChannelColor(object? value, object? parameter)
    {
        if (value is not HsvColor hsv || parameter is not HsvChannel channel)
        {
            return null;
        }

        return channel switch
        {
            HsvChannel.SMin => new HsvColor(hsv.Hue, 0, hsv.Value).ToColor(),
            HsvChannel.SMax => new HsvColor(hsv.Hue, 1, hsv.Value).ToColor(),
            HsvChannel.S => new HsvColor(hsv.Hue, hsv.Saturation, hsv.Value).ToColor(),
            HsvChannel.VMin => new HsvColor(hsv.Hue, hsv.Saturation, 0).ToColor(),
            HsvChannel.VMax => new HsvColor(hsv.Hue, hsv.Saturation, 1).ToColor(),
            HsvChannel.V => new HsvColor(hsv.Hue, hsv.Saturation, hsv.Value).ToColor(),
            HsvChannel.SvMax => new HsvColor(hsv.Hue, 1, 1).ToColor(),
            _ => null
        };
    }
}

public sealed class HsvChannelGradientBrushConverter : IValueConverter
{
    public static readonly HsvChannelGradientBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not HsvChannel channel)
        {
            return Binding.DoNothing;
        }

        Color? min;
        Color? max;
        switch (channel)
        {
            case HsvChannel.S:
                min = HsvChannelBrushConverter.ChannelColor(value, HsvChannel.SMin);
                max = HsvChannelBrushConverter.ChannelColor(value, HsvChannel.SMax);
                break;
            case HsvChannel.V:
                min = HsvChannelBrushConverter.ChannelColor(value, HsvChannel.VMin);
                max = HsvChannelBrushConverter.ChannelColor(value, HsvChannel.VMax);
                break;
            default:
                Trace.TraceWarning($"Unexpected HSV channel {channel}.");
                return Binding.DoNothing;
        }

        return min is { } minColor && max is { } maxColor ? Gradient(minColor, maxColor) : Binding.DoNothing;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    internal static LinearGradientBrush Gradient(Color min, Color max)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        brush.GradientStops.Add(new GradientStop(min, 0));
        brush.GradientStops.Add(new GradientStop(max, 1));
        brush.Freeze();
        return brush;
    }
}

public sealed class ColorChannelGradientBrushConverter : IValueConverter
{
    public static readonly ColorChannelGradientBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Color color || parameter is not ColorChannel channel)
        {
            return Binding.DoNothing;
        }

        Color min;
        Color max;
        switch (channel)
        {
            case ColorChannel.R:
                min = Color.FromRgb(0, color.G, color.B);
                max = Color.FromRgb(255, color.G, color.B);
                break;
            case ColorChannel.G:
                min = Color.FromRgb(color.R, 0, color.B);
                max = Color.FromRgb(color.R, 255, color.B);
                break;
            case ColorChannel.B:
                min = Color.FromRgb(color.R, color.G, 0);
                max = Color.FromRgb(color.R, color.G, 255);
                break;
            case ColorChannel.A:
                min = Color.FromArgb(0, color.R, color.G, color.B);
                max = Color.FromArgb(255, color.R, color.G, color.B);
                break;
            default:
                return Binding.DoNothing;
        }

        return HsvChannelGradientBrushConverter.Gradient(min, max);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Rounded clip for a border's content. Adapted from MahApps.Metro.
/// </summary>
public sealed class ClipGeometryConverter : IMultiValueConverter
{
    public static readonly ClipGeometryConverter Instance = new();

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2
            || values[0] is not double width
            || values[1] is not double height
            || width < 1
            || height < 1)
        {
            return Geometry.Empty;
        }

        var cornerRadius = values.Length > 2 && values[2] is CornerRadius radius ? radius : default;
        var borderThickness = values.Length > 3 && values[3] is Thickness thickness ? thickness : default;
        var padding = values.Length > 4 && values[4] is Thickness pad ? pad : default;
        var inset = new Thickness(
            (0.5 * borderThickness.Left) + padding.Left,
            (0.5 * borderThickness.Top) + padding.Top,
            (0.5 * borderThickness.Right) + padding.Right,
            (0.5 * borderThickness.Bottom) + padding.Bottom);
        var geometry = GetRoundRectangle(new Rect(0, 0, width, height), inset, cornerRadius);
        geometry.Freeze();
        return geometry;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static Geometry GetRoundRectangle(Rect baseRect, Thickness inset, CornerRadius cornerRadius)
    {
        var topLeft = Corner(cornerRadius.TopLeft - inset.Left, cornerRadius.TopLeft - inset.Top);
        var topRight = Corner(cornerRadius.TopRight - inset.Right, cornerRadius.TopRight - inset.Top);
        var bottomRight = Corner(cornerRadius.BottomRight - inset.Right, cornerRadius.BottomRight - inset.Bottom);
        var bottomLeft = Corner(cornerRadius.BottomLeft - inset.Left, cornerRadius.BottomLeft - inset.Bottom);

        topLeft.Location = baseRect.TopLeft;
        topRight.Location = new Point(baseRect.Right - topRight.Width, baseRect.Top);
        bottomRight.Location = new Point(baseRect.Right - bottomRight.Width, baseRect.Bottom - bottomRight.Height);
        bottomLeft.Location = new Point(baseRect.Left, baseRect.Bottom - bottomLeft.Height);

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(topLeft.BottomLeft, true, true);
            context.ArcTo(topLeft.TopRight, topLeft.Size, 0, false, SweepDirection.Clockwise, true, true);
            context.LineTo(topRight.TopLeft, true, true);
            context.ArcTo(topRight.BottomRight, topRight.Size, 0, false, SweepDirection.Clockwise, true, true);
            context.LineTo(bottomRight.TopRight, true, true);
            context.ArcTo(bottomRight.BottomLeft, bottomRight.Size, 0, false, SweepDirection.Clockwise, true, true);
            context.LineTo(bottomLeft.BottomRight, true, true);
            context.ArcTo(bottomLeft.TopLeft, bottomLeft.Size, 0, false, SweepDirection.Clockwise, true, true);
        }

        return geometry;
    }

    private static Rect Corner(double width, double height)
    {
        return new Rect(0, 0, Math.Max(0, width), Math.Max(0, height));
    }
}
