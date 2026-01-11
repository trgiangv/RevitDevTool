using RevitDevTool.RichTextBox.Colored.Rtf;

namespace RevitDevTool.RichTextBox.Colored.Rendering;

public class NewLineTokenRenderer : ITokenRenderer
{
    public void Render(ZLoggerLogEntry logEntry, IRtfCanvas canvas)
    {
        canvas.AppendText(Environment.NewLine);
    }
}