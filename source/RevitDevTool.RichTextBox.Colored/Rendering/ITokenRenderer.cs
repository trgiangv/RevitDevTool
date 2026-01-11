using RevitDevTool.RichTextBox.Colored.Rtf;

namespace RevitDevTool.RichTextBox.Colored.Rendering;

public interface ITokenRenderer
{
    void Render(ZLoggerLogEntry logEntry, IRtfCanvas canvas);
}