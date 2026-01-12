using RevitDevTool.Logging.Theme;
using RevitDevTool.RichTextBox.Colored.Themes;
using InternalTimeInterval = RevitDevTool.Models.Config.RollingInterval;
using ZLoggerTheme = RevitDevTool.RichTextBox.Colored.Themes.Theme;
using ZloggerTimeInterval = ZLogger.Providers.RollingInterval;

namespace RevitDevTool.Logging.ZLogger;

public static class ZloggerUtils
{
    public static ZloggerTimeInterval ToZlogger(this InternalTimeInterval interval)
    {
        return interval switch
        {
            InternalTimeInterval.Infinite => ZloggerTimeInterval.Infinite,
            InternalTimeInterval.Year => ZloggerTimeInterval.Year,
            InternalTimeInterval.Month => ZloggerTimeInterval.Month,
            InternalTimeInterval.Day => ZloggerTimeInterval.Day,
            InternalTimeInterval.Hour => ZloggerTimeInterval.Hour,
            InternalTimeInterval.Minute => ZloggerTimeInterval.Minute,
            _ => ZloggerTimeInterval.Day
        };
    }

    public static ZLoggerTheme ToZLoggerTheme(this LogTheme logTheme)
    {
        var defaultStyle = new Style(logTheme.DefaultStyle.Foreground, logTheme.DefaultStyle.Background);

        var styles = new Dictionary<StyleToken, Style>();

        foreach (var kvp in logTheme.Styles)
        {
            var zloggerToken = MapToken(kvp.Key);
            styles[zloggerToken] = new Style(kvp.Value.Foreground, kvp.Value.Background);
        }

        return new ZLoggerTheme(defaultStyle, styles);
    }

    private static StyleToken MapToken(LogStyleToken token)
    {
        return token switch
        {
            LogStyleToken.Text => StyleToken.Text,
            LogStyleToken.SecondaryText => StyleToken.SecondaryText,
            LogStyleToken.TertiaryText => StyleToken.TertiaryText,
            LogStyleToken.Invalid => StyleToken.Invalid,
            LogStyleToken.Null => StyleToken.Null,
            LogStyleToken.Name => StyleToken.Name,
            LogStyleToken.String => StyleToken.String,
            LogStyleToken.Number => StyleToken.Number,
            LogStyleToken.Boolean => StyleToken.Boolean,
            LogStyleToken.Scalar => StyleToken.Scalar,
            LogStyleToken.LevelVerbose => StyleToken.LevelVerbose,
            LogStyleToken.LevelDebug => StyleToken.LevelDebug,
            LogStyleToken.LevelInformation => StyleToken.LevelInformation,
            LogStyleToken.LevelWarning => StyleToken.LevelWarning,
            LogStyleToken.LevelError => StyleToken.LevelError,
            LogStyleToken.LevelFatal => StyleToken.LevelFatal,
            _ => StyleToken.Text
        };
    }
}