using RevitDevTool.Logging.Theme;
using SerilogTheme = Serilog.Sinks.RichTextBoxForms.Themes.Theme;
using SerilogStyle = Serilog.Sinks.RichTextBoxForms.Themes.Style;
using SerilogStyleToken = Serilog.Sinks.RichTextBoxForms.Themes.StyleToken;

namespace RevitDevTool.Logging.Serilog;

/// <summary>
/// Converts library-agnostic LogTheme to Serilog-specific Theme.
/// </summary>
internal static class SerilogThemeAdapter
{
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
