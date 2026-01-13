using ZLogger.RichTextBox.Winforms.Rtf;
using ZLogger.RichTextBox.Winforms.Themes;

namespace ZLogger.RichTextBox.Winforms.Rendering;

public class MessageTokenRenderer(Theme theme) : ITokenRenderer
{
    public void Render(ZLoggerLogEntry logEntry, IRtfCanvas canvas)
    {
        theme.Render(canvas, StyleToken.Text, logEntry.Message);
    }
}
