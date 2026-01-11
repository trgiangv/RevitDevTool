using RevitDevTool.RichTextBox.Colored.Rtf;
using RevitDevTool.RichTextBox.Colored.Themes;

namespace RevitDevTool.RichTextBox.Colored.Rendering;

public class MessageTokenRenderer : ITokenRenderer
{
    private readonly Theme _theme;

    public MessageTokenRenderer(Theme theme)
    {
        _theme = theme;
    }

    public void Render(ZLoggerLogEntry logEntry, IRtfCanvas canvas)
    {
        _theme.Render(canvas, StyleToken.Text, logEntry.Message);
    }
}
