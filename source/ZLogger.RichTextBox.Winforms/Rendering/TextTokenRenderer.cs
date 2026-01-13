using ZLogger.RichTextBox.Winforms.Rtf;
using ZLogger.RichTextBox.Winforms.Themes;

namespace ZLogger.RichTextBox.Winforms.Rendering;

public class TextTokenRenderer(Theme theme, string text) : ITokenRenderer
{
    public void Render(ZLoggerLogEntry logEntry, IRtfCanvas canvas)
    {
        theme.Render(canvas, StyleToken.TertiaryText, text);
    }
}