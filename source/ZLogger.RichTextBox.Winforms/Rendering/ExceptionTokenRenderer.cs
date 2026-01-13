using ZLogger.RichTextBox.Winforms.Rtf;
using ZLogger.RichTextBox.Winforms.Themes;

namespace ZLogger.RichTextBox.Winforms.Rendering;

public class ExceptionTokenRenderer(Theme theme) : ITokenRenderer
{
    private const string StackFrameLinePrefix = "   ";

    public void Render(ZLoggerLogEntry logEntry, IRtfCanvas canvas)
    {
        if (logEntry.Exception == null) return;

        var exceptionString = logEntry.Exception.ToString().AsSpan();
        var position = 0;

        while (position < exceptionString.Length)
        {
            var newlineIndex = exceptionString.Slice(position).IndexOf('\n');
            var lineSpan = newlineIndex >= 0
                ? exceptionString.Slice(position, newlineIndex)
                : exceptionString.Slice(position);

            if (lineSpan.Length > 0)
            {
                var line = lineSpan.ToString();
                var isStackFrame = line.StartsWith(StackFrameLinePrefix);

                var style = isStackFrame ? StyleToken.SecondaryText : StyleToken.Text;
                theme.Render(canvas, style, line);
            }

            canvas.AppendText(Environment.NewLine);

            position += (newlineIndex >= 0 ? newlineIndex + 1 : lineSpan.Length);
            if (newlineIndex < 0) break;
        }
    }
}