using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace RevitDevTool.Desktop.Converters;

public class StatusToColorConverter : IValueConverter
{
    public static readonly StatusToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString()?.ToLowerInvariant() ?? "";

        return status switch
        {
            "completed" or "success" or "done" => new SolidColorBrush(Color.Parse("#10B981")), // Green
            "failed" or "error" or "exception" => new SolidColorBrush(Color.Parse("#EF4444")), // Red
            "running" or "processing" or "in progress" => new SolidColorBrush(Color.Parse("#F59E0B")), // Amber
            "queued" or "pending" or "waiting" => new SolidColorBrush(Color.Parse("#6B7280")), // Gray
            "skipped" => new SolidColorBrush(Color.Parse("#8B5CF6")), // Purple
            _ => new SolidColorBrush(Color.Parse("#3B82F6")) // Default Blue
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

