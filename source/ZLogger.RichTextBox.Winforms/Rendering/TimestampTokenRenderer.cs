using ZLogger.RichTextBox.Winforms.Rtf;
using ZLogger.RichTextBox.Winforms.Themes;

namespace ZLogger.RichTextBox.Winforms.Rendering;

public class TimestampTokenRenderer(Theme theme, string format, IFormatProvider? formatProvider = null) : ITokenRenderer
{
    public void Render(ZLoggerLogEntry logEntry, IRtfCanvas canvas)
    {
        var formattedTimestamp = logEntry.Timestamp.ToString(format, formatProvider);
        theme.Render(canvas, StyleToken.SecondaryText, formattedTimestamp);
    }
}