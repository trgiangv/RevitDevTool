// ReSharper disable ConvertToPrimaryConstructor
namespace RevitDevTool.Logging.Theme;

/// <summary>
/// Represents a complete log theme with default style and token-specific styles.
/// Library-agnostic representation that can be converted to Serilog or ZLogger themes.
/// </summary>
public sealed class LogTheme
{
    public LogStyle DefaultStyle { get; }
    public IReadOnlyDictionary<LogStyleToken, LogStyle> Styles { get; }

    public LogTheme(LogStyle defaultStyle, Dictionary<LogStyleToken, LogStyle> styles)
    {
        DefaultStyle = defaultStyle;
        Styles = styles;
    }
}
