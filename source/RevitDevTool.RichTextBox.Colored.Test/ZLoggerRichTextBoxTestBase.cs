using Microsoft.Extensions.Logging;
using RevitDevTool.RichTextBox.Colored.Rendering;
using RevitDevTool.RichTextBox.Colored.Themes;

namespace RevitDevTool.RichTextBox.Colored.Tests;

public abstract class ZLoggerRichTextBoxTestBase : IDisposable
{
    protected readonly System.Windows.Forms.RichTextBox _richTextBox;
    protected readonly Theme _defaultTheme;
    protected readonly ZLoggerTemplateRenderer _renderer;
    protected readonly RichTextBoxCanvasAdapter _canvas;
    protected readonly ZLoggerRichTextBoxOptions _options;
    protected bool _disposed;

    protected ZLoggerRichTextBoxTestBase()
    {
        _richTextBox = new System.Windows.Forms.RichTextBox();
        _defaultTheme = ThemePresets.Literate;

        _options = new ZLoggerRichTextBoxOptions
        {
            Theme = _defaultTheme,
            OutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message}{NewLine}{Exception}",
            AutoScroll = true,
            MaxLogLines = 1000
        };

        _renderer = new ZLoggerTemplateRenderer(_options);

        // Wrap the RichTextBox in an IRtfCanvas adapter so that unit tests
        // can interact with the updated rendering API without extensive
        // rewrites.
        _canvas = new RichTextBoxCanvasAdapter(_richTextBox);
    }

    protected string RenderAndGetText(ZLoggerLogEntry logEntry)
    {
        _richTextBox.Clear();
        _renderer.Render(logEntry, _canvas);
        return _richTextBox.Text.TrimEnd('\n', '\r');
    }

    protected string RenderAndGetText(ZLoggerLogEntry logEntry, string outputTemplate)
    {
        _richTextBox.Clear();
        var options = new ZLoggerRichTextBoxOptions
        {
            Theme = _defaultTheme,
            OutputTemplate = outputTemplate
        };
        var renderer = new ZLoggerTemplateRenderer(options);
        renderer.Render(logEntry, _canvas);
        return _richTextBox.Text.TrimEnd('\n', '\r');
    }

    protected static ZLoggerLogEntry CreateLogEntry(
        LogLevel level,
        string message,
        Exception? exception = null,
        Dictionary<string, object?>? properties = null)
    {
        return new ZLoggerLogEntry(
            level,
            DateTimeOffset.Now,
            "TestCategory",
            message,
            exception,
            properties);
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
        if (!_disposed)
        {
            _richTextBox?.Dispose();
            _disposed = true;
        }
    }
}
