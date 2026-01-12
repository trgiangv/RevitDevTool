using Microsoft.Extensions.Logging;
using RevitDevTool.RichTextBox.Colored.Formatting;
using RevitDevTool.RichTextBox.Colored.Rtf;
using RevitDevTool.RichTextBox.Colored.Themes;

namespace RevitDevTool.RichTextBox.Colored.Rendering;

public class LevelTokenRenderer : ITokenRenderer
{
    private static readonly StyleToken[] LevelStyles =
    [
        StyleToken.LevelVerbose,     // Trace
        StyleToken.LevelDebug,       // Debug
        StyleToken.LevelInformation, // Information
        StyleToken.LevelWarning,     // Warning
        StyleToken.LevelError,       // Error
        StyleToken.LevelFatal        // Critical
    ];

    private static readonly string[][] LowercaseLevelMap =
    [
        ["t", "tr", "trc", "trce"],  // Trace
        ["d", "de", "dbg", "dbug"],  // Debug
        ["i", "in", "inf", "info"],  // Information
        ["w", "wn", "wrn", "warn"],  // Warning
        ["e", "er", "err", "eror"],  // Error
        ["c", "cr", "crt", "crit"]   // Critical
    ];

    private static readonly string[][] TitleCaseLevelMap =
    [
        ["T", "Tr", "Trc", "Trce"],  // Trace
        ["D", "De", "Dbg", "Dbug"],  // Debug
        ["I", "In", "Inf", "Info"],  // Information
        ["W", "Wn", "Wrn", "Warn"],  // Warning
        ["E", "Er", "Err", "Eror"],  // Error
        ["C", "Cr", "Crt", "Crit"]   // Critical
    ];

    private static readonly string[][] UppercaseLevelMap =
    [
        ["T", "TR", "TRC", "TRCE"],  // Trace
        ["D", "DE", "DBG", "DBUG"],  // Debug
        ["I", "IN", "INF", "INFO"],  // Information
        ["W", "WN", "WRN", "WARN"],  // Warning
        ["E", "ER", "ERR", "EROR"],  // Error
        ["C", "CR", "CRT", "CRIT"]   // Critical
    ];

    private readonly Theme _theme;
    private readonly string _format;
    private readonly string[] _monikers;

    public LevelTokenRenderer(Theme theme, string format = "")
    {
        _theme = theme;
        _format = format;
        _monikers = new string[LevelStyles.Length];

        for (var i = 0; i < _monikers.Length; i++)
        {
            _monikers[i] = GetLevelMoniker((LogLevel)i, format);
        }
    }

    public void Render(ZLoggerLogEntry logEntry, IRtfCanvas canvas)
    {
        var levelIndex = (int)logEntry.Level;
        if (levelIndex < 0 || levelIndex >= _monikers.Length)
        {
            var fallbackMoniker = TextFormatter.Format(logEntry.Level.ToString(), _format);
            _theme.Render(canvas, StyleToken.Text, fallbackMoniker);
            return;
        }

        var moniker = _monikers[levelIndex];
        var levelStyle = LevelStyles[levelIndex];
        _theme.Render(canvas, levelStyle, moniker);
    }

    private static string GetLevelMoniker(LogLevel value, string format = "")
    {
        if (format.Length != 2 && format.Length != 3)
        {
            return TextFormatter.Format(value.ToString(), format);
        }

        var width = format[1] - '0';
        if (format.Length == 3)
        {
            width *= 10;
            width += format[2] - '0';
        }

        switch (width)
        {
            case < 1:
                return string.Empty;

            case > 4:
                {
                    var stringValue = value.ToString();
                    if (stringValue.Length > width)
                    {
                        stringValue = stringValue.Substring(0, width);
                    }

                    return TextFormatter.Format(stringValue, format);
                }
        }

        var index = (int)value;
        if (index < 0 || index >= LowercaseLevelMap.Length)
        {
            return TextFormatter.Format(value.ToString(), format);
        }

        return format[0] switch
        {
            'w' => LowercaseLevelMap[index][width - 1],
            'u' => UppercaseLevelMap[index][width - 1],
            't' => TitleCaseLevelMap[index][width - 1],
            _ => TextFormatter.Format(value.ToString(), format)
        };
    }
}