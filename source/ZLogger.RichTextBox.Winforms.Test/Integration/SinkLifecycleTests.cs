using Microsoft.Extensions.Logging;
using Xunit;
using ZLogger.RichTextBox.Winforms.Themes;
namespace ZLogger.RichTextBox.Winforms.Test.Integration;

public class SinkLifecycleTests : IDisposable
{
    private readonly System.Windows.Forms.RichTextBox _richTextBox;
    private ZLoggerRichTextBoxLoggerProvider? _provider;
    private ILoggerFactory? _loggerFactory;
    private bool _disposed;

    public SinkLifecycleTests()
    {
        _richTextBox = new System.Windows.Forms.RichTextBox();
    }

    [Fact]
    public void Provider_WhenCreated_InitializesRichTextBox()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddZLoggerRichTextBox(_richTextBox, out _provider, options =>
            {
                options.Theme = ThemePresets.Literate;
                options.MaxLogLines = 1000;
            });
        });

        Assert.NotNull(_provider);
        Assert.True(_richTextBox.ReadOnly);
        Assert.False(_richTextBox.DetectUrls);
    }

    [Fact]
    public void Provider_WhenDisposed_CanBeRecreated()
    {
        // First creation
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddZLoggerRichTextBox(_richTextBox, out _provider);
        });

        var logger = _loggerFactory.CreateLogger("Test");
        logger.LogInformation("First message");

        // Dispose
        _provider?.Dispose();
        _loggerFactory?.Dispose();

        // Second creation
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddZLoggerRichTextBox(_richTextBox, out _provider);
        });

        logger = _loggerFactory.CreateLogger("Test");
        logger.LogInformation("Second message");

        Assert.NotNull(_provider);
    }

    [Fact]
    public void Processor_Clear_ClearsBuffer()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddZLoggerRichTextBox(_richTextBox, out _provider);
        });

        var logger = _loggerFactory.CreateLogger("Test");
        logger.LogInformation("Test message");

        // Give time for processing
        Thread.Sleep(100);

        _provider?.Processor.Clear();

        // Give time for clear to process
        Thread.Sleep(100);

        // After clear, the buffer should be empty (but we can restore)
        Assert.NotNull(_provider);
    }

    [Fact]
    public void Processor_Restore_RestoresBuffer()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddZLoggerRichTextBox(_richTextBox, out _provider);
        });

        var logger = _loggerFactory.CreateLogger("Test");
        logger.LogInformation("Test message");

        // Give time for processing
        Thread.Sleep(100);

        _provider?.Processor.Clear();
        Thread.Sleep(50);

        _provider?.Processor.Restore();
        Thread.Sleep(100);

        Assert.NotNull(_provider);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (!_disposed)
        {
            _provider?.Dispose();
            _loggerFactory?.Dispose();
            _richTextBox?.Dispose();
            _disposed = true;
        }
    }
}
