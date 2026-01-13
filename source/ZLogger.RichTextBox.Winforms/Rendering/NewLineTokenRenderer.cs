using ZLogger.RichTextBox.Winforms.Rtf;

namespace ZLogger.RichTextBox.Winforms.Rendering;

public class NewLineTokenRenderer : ITokenRenderer
{
    public void Render(ZLoggerLogEntry logEntry, IRtfCanvas canvas)
    {
        canvas.AppendText(Environment.NewLine);
    }
}