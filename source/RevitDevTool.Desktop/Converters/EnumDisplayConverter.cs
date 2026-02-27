using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace RevitDevTool.Desktop.Converters;

/// <summary>
/// Converts a PascalCase enum value to a human-readable display string.
/// e.g. DetachAndPreserveWorksets → "Detach And Preserve Worksets"
///      SequentialMulti → "Sequential Multi"
/// </summary>
public sealed class EnumDisplayConverter : IValueConverter
{
    public static readonly EnumDisplayConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return null;
        var name = value.ToString() ?? string.Empty;
        return Regex.Replace(name, "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
