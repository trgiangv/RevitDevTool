using Microsoft.Extensions.Logging;
using RevitDevTool.Logging.Theme;
using Serilog.Events;
using InternalTimeInterval = RevitDevTool.Logging.Enums.RollingInterval;
using SerilogStyle = Serilog.Sinks.RichTextBoxForms.Themes.Style;
using SerilogStyleToken = Serilog.Sinks.RichTextBoxForms.Themes.StyleToken;
using SerilogTheme = Serilog.Sinks.RichTextBoxForms.Themes.Theme;
using SerilogTimeInterval = Serilog.RollingInterval;
namespace RevitDevTool.Logging.Serilog;

public static class SerilogExtensions
{
    public static LogEventLevel ToSerilog(this LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            _ => LogEventLevel.Debug
        };
    }

    public static SerilogTimeInterval ToSerilog(this InternalTimeInterval interval)
    {
        return interval switch
        {
            InternalTimeInterval.Infinite => SerilogTimeInterval.Infinite,
            InternalTimeInterval.Year => SerilogTimeInterval.Year,
            InternalTimeInterval.Month => SerilogTimeInterval.Month,
            InternalTimeInterval.Day => SerilogTimeInterval.Day,
            InternalTimeInterval.Hour => SerilogTimeInterval.Hour,
            InternalTimeInterval.Minute => SerilogTimeInterval.Minute,
            _ => SerilogTimeInterval.Day
        };
    }

    public static SerilogTheme ToSerilogTheme(this LogTheme logTheme)
    {
        var defaultStyle = new SerilogStyle(logTheme.DefaultStyle.Foreground, logTheme.DefaultStyle.Background);

        var styles = new Dictionary<SerilogStyleToken, SerilogStyle>();

        foreach (var kvp in logTheme.Styles)
        {
            var serilogToken = MapToken(kvp.Key);
            styles[serilogToken] = new SerilogStyle(kvp.Value.Foreground, kvp.Value.Background);
        }

        return new SerilogTheme(defaultStyle, styles);
    }

    private static SerilogStyleToken MapToken(LogStyleToken token)
    {
        return token switch
        {
            LogStyleToken.Text => SerilogStyleToken.Text,
            LogStyleToken.SecondaryText => SerilogStyleToken.SecondaryText,
            LogStyleToken.TertiaryText => SerilogStyleToken.TertiaryText,
            LogStyleToken.Invalid => SerilogStyleToken.Invalid,
            LogStyleToken.Null => SerilogStyleToken.Null,
            LogStyleToken.Name => SerilogStyleToken.Name,
            LogStyleToken.String => SerilogStyleToken.String,
            LogStyleToken.Number => SerilogStyleToken.Number,
            LogStyleToken.Boolean => SerilogStyleToken.Boolean,
            LogStyleToken.Scalar => SerilogStyleToken.Scalar,
            LogStyleToken.LevelVerbose => SerilogStyleToken.LevelVerbose,
            LogStyleToken.LevelDebug => SerilogStyleToken.LevelDebug,
            LogStyleToken.LevelInformation => SerilogStyleToken.LevelInformation,
            LogStyleToken.LevelWarning => SerilogStyleToken.LevelWarning,
            LogStyleToken.LevelError => SerilogStyleToken.LevelError,
            LogStyleToken.LevelFatal => SerilogStyleToken.LevelFatal,
            _ => SerilogStyleToken.Text
        };
    }
}