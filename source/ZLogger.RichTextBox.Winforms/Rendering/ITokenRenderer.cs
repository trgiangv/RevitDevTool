using ZLogger.RichTextBox.Winforms.Rtf;

namespace ZLogger.RichTextBox.Winforms.Rendering;

public interface ITokenRenderer
{
    void Render(ZLoggerLogEntry logEntry, IRtfCanvas canvas);
}