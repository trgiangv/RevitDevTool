using ZLogger.RichTextBox.Winforms.Rtf;
using ZLogger.RichTextBox.Winforms.Themes;

namespace ZLogger.RichTextBox.Winforms.Rendering;

public class CategoryTokenRenderer(Theme theme) : ITokenRenderer
{
    public void Render(ZLoggerLogEntry logEntry, IRtfCanvas canvas)
    {
        theme.Render(canvas, StyleToken.Name, logEntry.Category);
    }
}
