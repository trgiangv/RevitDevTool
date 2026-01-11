using RevitDevTool.RichTextBox.Colored.Rtf;
using RevitDevTool.RichTextBox.Colored.Themes;

namespace RevitDevTool.RichTextBox.Colored.Rendering;

public class TimestampTokenRenderer : ITokenRenderer
{
    private readonly Theme _theme;
    private readonly string _format;
    private readonly IFormatProvider? _formatProvider;

    public TimestampTokenRenderer(Theme theme, string format, IFormatProvider? formatProvider = null)
    {
        _theme = theme;
        _format = format;
        _formatProvider = formatProvider;
    }

    public void Render(ZLoggerLogEntry logEntry, IRtfCanvas canvas)
    {
        var formattedTimestamp = logEntry.Timestamp.ToString(_format, _formatProvider);
        _theme.Render(canvas, StyleToken.SecondaryText, formattedTimestamp);
    }
}