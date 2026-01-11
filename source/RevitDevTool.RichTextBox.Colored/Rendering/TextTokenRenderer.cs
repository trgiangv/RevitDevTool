using RevitDevTool.RichTextBox.Colored.Rtf;
using RevitDevTool.RichTextBox.Colored.Themes;

namespace RevitDevTool.RichTextBox.Colored.Rendering;

public class TextTokenRenderer : ITokenRenderer
{
    private readonly string _text;
    private readonly Theme _theme;

    public TextTokenRenderer(Theme theme, string text)
    {
        _theme = theme;
        _text = text;
    }

    public void Render(ZLoggerLogEntry logEntry, IRtfCanvas canvas)
    {
        _theme.Render(canvas, StyleToken.TertiaryText, _text);
    }
}