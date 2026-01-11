using RevitDevTool.RichTextBox.Colored.Rtf;
using RevitDevTool.RichTextBox.Colored.Themes;

namespace RevitDevTool.RichTextBox.Colored.Rendering;

public class CategoryTokenRenderer : ITokenRenderer
{
    private readonly Theme _theme;

    public CategoryTokenRenderer(Theme theme)
    {
        _theme = theme;
    }

    public void Render(ZLoggerLogEntry logEntry, IRtfCanvas canvas)
    {
        _theme.Render(canvas, StyleToken.Name, logEntry.Category);
    }
}
