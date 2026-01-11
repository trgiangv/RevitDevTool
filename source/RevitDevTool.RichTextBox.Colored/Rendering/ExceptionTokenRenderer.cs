using RevitDevTool.RichTextBox.Colored.Rtf;
using RevitDevTool.RichTextBox.Colored.Themes;

namespace RevitDevTool.RichTextBox.Colored.Rendering;

public class ExceptionTokenRenderer : ITokenRenderer
{
    private const string StackFrameLinePrefix = "   ";
    private readonly Theme _theme;

    public ExceptionTokenRenderer(Theme theme)
    {
        _theme = theme;
    }

    public void Render(ZLoggerLogEntry logEntry, IRtfCanvas canvas)
    {
        if (logEntry.Exception == null) return;

        var lines = new StringReader(logEntry.Exception.ToString());
        while (lines.ReadLine() is { } nextLine)
        {
            var style = nextLine.StartsWith(StackFrameLinePrefix) ? StyleToken.SecondaryText : StyleToken.Text;
            _theme.Render(canvas, style, nextLine);
            canvas.AppendText(Environment.NewLine);
        }
    }
}