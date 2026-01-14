using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.RichTextBoxForms.Themes;
using ZLogger.RichTextBox.Winforms;
using ZLogger.RichTextBox.Winforms.Themes;
using SerilogThemePresets = Serilog.Sinks.RichTextBoxForms.Themes.ThemePresets;
using ZLoggerThemePresets = ZLogger.RichTextBox.Winforms.Themes.ThemePresets;
using WinFormsRichTextBox = System.Windows.Forms.RichTextBox;

namespace ZLogger.RichTextBox.Winforms.Benchmark;

/// <summary>
/// Base class providing common setup for Serilog and ZLogger RichTextBox benchmarks.
/// Creates isolated RichTextBox controls for each framework.
/// </summary>
public abstract class RichTextBoxBenchmarkBase : IDisposable
{
    // Serilog components
    protected WinFormsRichTextBox SerilogRichTextBox { get; private set; } = null!;
    protected Serilog.ILogger SerilogLogger { get; private set; } = null!;
    protected Serilog.Sinks.RichTextBoxForms.RichTextBoxSink? SerilogSink { get; private set; }

    // ZLogger components
    protected WinFormsRichTextBox ZLoggerRichTextBox { get; private set; } = null!;
    protected Microsoft.Extensions.Logging.ILogger ZLoggerLogger { get; private set; } = null!;
    protected ZLoggerRichTextBoxLoggerProvider? ZLoggerProvider { get; private set; }
    protected ILoggerFactory? ZLoggerFactory { get; private set; }

    protected int MaxLogLines { get; set; } = 1000;
    protected bool AutoScroll { get; set; } = true;

    public virtual void Setup()
    {
        SetupSerilog();
        SetupZLogger();
    }

    private void SetupSerilog()
    {
        SerilogRichTextBox = new WinFormsRichTextBox
        {
            Font = new Font("Cascadia Mono", 9f, FontStyle.Regular, GraphicsUnit.Point),
            ReadOnly = true,
            DetectUrls = false,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.None
        };

        var config = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.RichTextBox(
                SerilogRichTextBox,
                out var sink,
                theme: SerilogThemePresets.Literate,
                autoScroll: AutoScroll,
                maxLogLines: MaxLogLines);

        SerilogSink = sink;
        SerilogLogger = config.CreateLogger();
    }

    private void SetupZLogger()
    {
        ZLoggerRichTextBox = new WinFormsRichTextBox
        {
            Font = new Font("Cascadia Mono", 9f, FontStyle.Regular, GraphicsUnit.Point),
            ReadOnly = true,
            DetectUrls = false,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.None
        };

        ZLoggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
            builder.AddZLoggerRichTextBox(ZLoggerRichTextBox, out var provider, options =>
            {
                options.Theme = ZLoggerThemePresets.Literate;
                options.AutoScroll = AutoScroll;
                options.MaxLogLines = MaxLogLines;
            });
            ZLoggerProvider = provider;
        });

        ZLoggerLogger = ZLoggerFactory.CreateLogger("Benchmark");
    }

    public virtual void Cleanup()
    {
        // Dispose Serilog
        (SerilogLogger as IDisposable)?.Dispose();
        SerilogSink?.Dispose();
        SerilogRichTextBox?.Dispose();

        // Dispose ZLogger
        ZLoggerProvider?.Dispose();
        ZLoggerFactory?.Dispose();
        ZLoggerRichTextBox?.Dispose();
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}
