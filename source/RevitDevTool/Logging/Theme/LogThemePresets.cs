using Color = System.Drawing.Color;

namespace RevitDevTool.Logging.Theme;

/// <summary>
/// Provides predefined log themes that can be used with any logging library.
/// These themes are library-agnostic and will be converted to the appropriate
/// format by the logging adapters.
/// </summary>
public static class LogThemePresets
{
    public static readonly Color LightBackground = Color.FromArgb(250, 250, 250);
    public static readonly Color DarkBackground = Color.FromArgb(30, 30, 30);

    /// <summary>
    /// Enhanced dark theme with better contrast and readability.
    /// </summary>
    public static LogTheme EnhancedDark { get; } = new(
        new LogStyle(Color.FromArgb(240, 240, 240), DarkBackground),
        new Dictionary<LogStyleToken, LogStyle>
        {
            [LogStyleToken.Text] = new(Color.FromArgb(240, 240, 240), DarkBackground),
            [LogStyleToken.SecondaryText] = new(Color.FromArgb(180, 180, 180), DarkBackground),
            [LogStyleToken.TertiaryText] = new(Color.FromArgb(160, 160, 160), DarkBackground),
            [LogStyleToken.Invalid] = new(Color.FromArgb(255, 220, 120), DarkBackground),
            [LogStyleToken.Null] = new(Color.FromArgb(180, 180, 255), DarkBackground),
            [LogStyleToken.Name] = new(Color.FromArgb(255, 200, 255), DarkBackground),
            [LogStyleToken.String] = new(Color.FromArgb(120, 255, 170), DarkBackground),
            [LogStyleToken.Number] = new(Color.FromArgb(255, 200, 120), DarkBackground),
            [LogStyleToken.Boolean] = new(Color.FromArgb(170, 220, 255), DarkBackground),
            [LogStyleToken.Scalar] = new(Color.FromArgb(170, 255, 220), DarkBackground),
            [LogStyleToken.LevelVerbose] = new(Color.FromArgb(140, 140, 140), DarkBackground),
            [LogStyleToken.LevelDebug] = new(Color.FromArgb(170, 170, 170), DarkBackground),
            [LogStyleToken.LevelInformation] = new(Color.FromArgb(120, 170, 255), DarkBackground),
            [LogStyleToken.LevelWarning] = new(Color.FromArgb(255, 220, 70), DarkBackground),
            [LogStyleToken.LevelError] = new(Color.FromArgb(255, 100, 100), DarkBackground),
            [LogStyleToken.LevelFatal] = new(Color.FromArgb(255, 80, 80), DarkBackground)
        });

    /// <summary>
    /// Enhanced light theme with better contrast and readability.
    /// </summary>
    public static LogTheme EnhancedLight { get; } = new(
        new LogStyle(Color.FromArgb(40, 40, 40), LightBackground),
        new Dictionary<LogStyleToken, LogStyle>
        {
            [LogStyleToken.Text] = new(Color.FromArgb(40, 40, 40), LightBackground),
            [LogStyleToken.SecondaryText] = new(Color.FromArgb(80, 80, 80), LightBackground),
            [LogStyleToken.TertiaryText] = new(Color.FromArgb(120, 120, 120), LightBackground),
            [LogStyleToken.Invalid] = new(Color.FromArgb(180, 80, 0), LightBackground),
            [LogStyleToken.Null] = new(Color.FromArgb(80, 80, 200), LightBackground),
            [LogStyleToken.Name] = new(Color.FromArgb(150, 0, 150), LightBackground),
            [LogStyleToken.String] = new(Color.FromArgb(0, 120, 0), LightBackground),
            [LogStyleToken.Number] = new(Color.FromArgb(180, 80, 0), LightBackground),
            [LogStyleToken.Boolean] = new(Color.FromArgb(0, 80, 180), LightBackground),
            [LogStyleToken.Scalar] = new(Color.FromArgb(0, 140, 100), LightBackground),
            [LogStyleToken.LevelVerbose] = new(Color.FromArgb(120, 120, 120), LightBackground),
            [LogStyleToken.LevelDebug] = new(Color.FromArgb(80, 80, 80), LightBackground),
            [LogStyleToken.LevelInformation] = new(Color.FromArgb(0, 80, 180), LightBackground),
            [LogStyleToken.LevelWarning] = new(Color.FromArgb(200, 140, 0), LightBackground),
            [LogStyleToken.LevelError] = new(Color.FromArgb(200, 60, 60), LightBackground),
            [LogStyleToken.LevelFatal] = new(Color.FromArgb(150, 30, 30), LightBackground)
        });

    /// <summary>
    /// Soft dark theme with muted colors for extended use.
    /// </summary>
    public static LogTheme SoftDark { get; } = new(
        new LogStyle(Color.FromArgb(200, 200, 200), DarkBackground),
        new Dictionary<LogStyleToken, LogStyle>
        {
            [LogStyleToken.Text] = new(Color.FromArgb(200, 200, 200), DarkBackground),
            [LogStyleToken.SecondaryText] = new(Color.FromArgb(150, 150, 150), DarkBackground),
            [LogStyleToken.TertiaryText] = new(Color.FromArgb(120, 120, 120), DarkBackground),
            [LogStyleToken.Invalid] = new(Color.FromArgb(220, 180, 80), DarkBackground),
            [LogStyleToken.Null] = new(Color.FromArgb(130, 130, 220), DarkBackground),
            [LogStyleToken.Name] = new(Color.FromArgb(220, 160, 220), DarkBackground),
            [LogStyleToken.String] = new(Color.FromArgb(120, 200, 140), DarkBackground),
            [LogStyleToken.Number] = new(Color.FromArgb(220, 160, 100), DarkBackground),
            [LogStyleToken.Boolean] = new(Color.FromArgb(130, 180, 220), DarkBackground),
            [LogStyleToken.Scalar] = new(Color.FromArgb(140, 200, 180), DarkBackground),
            [LogStyleToken.LevelVerbose] = new(Color.FromArgb(100, 100, 100), DarkBackground),
            [LogStyleToken.LevelDebug] = new(Color.FromArgb(130, 130, 130), DarkBackground),
            [LogStyleToken.LevelInformation] = new(Color.FromArgb(120, 160, 220), DarkBackground),
            [LogStyleToken.LevelWarning] = new(Color.FromArgb(220, 180, 60), DarkBackground),
            [LogStyleToken.LevelError] = new(Color.FromArgb(180, 60, 60), DarkBackground),
            [LogStyleToken.LevelFatal] = new(Color.FromArgb(150, 40, 40), DarkBackground)
        });

    /// <summary>
    /// High contrast light theme for better accessibility.
    /// </summary>
    public static LogTheme HighContrastLight { get; } = new(
        new LogStyle(Color.FromArgb(20, 20, 20), LightBackground),
        new Dictionary<LogStyleToken, LogStyle>
        {
            [LogStyleToken.Text] = new(Color.FromArgb(20, 20, 20), LightBackground),
            [LogStyleToken.SecondaryText] = new(Color.FromArgb(60, 60, 60), LightBackground),
            [LogStyleToken.TertiaryText] = new(Color.FromArgb(100, 100, 100), LightBackground),
            [LogStyleToken.Invalid] = new(Color.FromArgb(180, 80, 0), LightBackground),
            [LogStyleToken.Null] = new(Color.FromArgb(60, 60, 180), LightBackground),
            [LogStyleToken.Name] = new(Color.FromArgb(120, 0, 120), LightBackground),
            [LogStyleToken.String] = new(Color.FromArgb(0, 100, 0), LightBackground),
            [LogStyleToken.Number] = new(Color.FromArgb(160, 60, 0), LightBackground),
            [LogStyleToken.Boolean] = new(Color.FromArgb(0, 60, 160), LightBackground),
            [LogStyleToken.Scalar] = new(Color.FromArgb(0, 120, 80), LightBackground),
            [LogStyleToken.LevelVerbose] = new(Color.FromArgb(100, 100, 100), LightBackground),
            [LogStyleToken.LevelDebug] = new(Color.FromArgb(60, 60, 60), LightBackground),
            [LogStyleToken.LevelInformation] = new(Color.FromArgb(0, 60, 160), LightBackground),
            [LogStyleToken.LevelWarning] = new(Color.FromArgb(255, 200, 80), LightBackground),
            [LogStyleToken.LevelError] = new(Color.FromArgb(180, 40, 40), LightBackground),
            [LogStyleToken.LevelFatal] = new(Color.FromArgb(120, 20, 20), LightBackground)
        });
}
