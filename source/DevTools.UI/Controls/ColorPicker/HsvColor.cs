using System.Windows.Media;
// ReSharper disable once CheckNamespace
namespace DevTools.UI.Controls;

/// <summary>
/// A color in HSV (hue, saturation, value). Adapted from MahApps.Metro.
/// </summary>
public readonly struct HsvColor : IEquatable<HsvColor>
{
    public double A { get; }

    public double Hue { get; }

    public double Saturation { get; }

    public double Value { get; }

    public HsvColor(Color color)
    {
        A = color.A / 255d;
        Hue = 0;
        Saturation = 0;
        Value = 0;

        var max = Math.Max(color.R, Math.Max(color.G, color.B));
        var min = Math.Min(color.R, Math.Min(color.G, color.B));
        var delta = max - min;

        if (delta == 0)
        {
            // Hue stays 0 for gray.
        }
        else if (max == color.R)
        {
            Hue = 60 * ((double)(color.G - color.B) / delta % 6);
        }
        else if (max == color.G)
        {
            Hue = 60 * (2 + (double)(color.B - color.R) / delta);
        }
        else if (max == color.B)
        {
            Hue = 60 * (4 + (double)(color.R - color.G) / delta);
        }

        if (Hue < 0)
        {
            Hue += 360;
        }

        Saturation = max == 0 ? 0 : (double)delta / max;
        Value = max / 255d;
    }

    public HsvColor(double hue, double saturation, double value)
        : this(1, hue, saturation, value)
    {
    }

    public HsvColor(double a, double hue, double saturation, double value)
    {
        A = a;
        Hue = hue;
        Saturation = saturation;
        Value = value;
    }

    public Color ToColor()
    {
        return Color.FromArgb(
            (byte)Math.Round(A * 255),
            GetColorComponent(5),
            GetColorComponent(3),
            GetColorComponent(1));
    }

    public bool Equals(HsvColor other)
    {
        return AreClose(Hue, other.Hue)
               && AreClose(A, other.A)
               && AreClose(Saturation, other.Saturation)
               && AreClose(Value, other.Value);
    }

    public override bool Equals(object? obj)
    {
        return obj is HsvColor other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + Hue.GetHashCode();
            hash = (hash * 31) + A.GetHashCode();
            hash = (hash * 31) + Saturation.GetHashCode();
            hash = (hash * 31) + Value.GetHashCode();
            return hash;
        }
    }

    private byte GetColorComponent(int n)
    {
        var k = (n + (Hue / 60d)) % 6;
        return (byte)Math.Round((Value - (Value * Saturation * Math.Max(0, Math.Min(k, Math.Min(4 - k, 1))))) * 255);
    }

    private static bool AreClose(double left, double right)
    {
        return Math.Abs(left - right) < 1e-10;
    }
}
