using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Benchmarks.Benchmarking;
using RevitDevTool.Scintilla.Benchmarks.Scenarios;
using RevitDevTool.Scintilla.Control;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Extensions;
using Serilog;
using SerilogThemePresets = Serilog.Sinks.RichTextBoxForms.Themes.ThemePresets;
using System.Text;
using System.Windows.Forms;
using ZLogger;

namespace RevitDevTool.Scintilla.Benchmarks.Benchmarks;

[Config(typeof(InProcessBenchmarkConfig))]
[MemoryDiagnoser]
public class ComboPixelDrawBenchmarks
{
    private readonly IReadOnlyList<string> _messages =
        ScenarioDataFactory.BuildMessages(3000, 512, TokenDensity.High, structuredPayload: true, seed: 404);

    private RichTextBox _richTextBox = null!;
    private Serilog.ILogger _serilogRichTextLogger = null!;
    private ComboBenchmarkSupport.OffscreenPainter _richTextPainter = null!;

    private ScintillaLogViewer _scintillaViewer = null!;
    private Microsoft.Extensions.Logging.ILogger _zloggerScintillaLogger = null!;
    private Microsoft.Extensions.Logging.ILoggerFactory _zloggerScintillaFactory = null!;
    private ComboBenchmarkSupport.OffscreenPainter _scintillaPainter = null!;

    [GlobalSetup]
    public void Setup()
    {
        _richTextBox = new RichTextBox();
        ComboBenchmarkSupport.PrepareOffscreenControl(_richTextBox);
        _richTextPainter = new ComboBenchmarkSupport.OffscreenPainter(_richTextBox.ClientSize.Width, _richTextBox.ClientSize.Height);
        _serilogRichTextLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.RichTextBox(
                _richTextBox,
                theme: SerilogThemePresets.EnhancedDark,
                autoScroll: false,
                maxLogLines: 300_000,
                prettyPrintJson: true,
                enableTokenLinks: false,
                enableAutoTokenDetection: false)
            .CreateLogger();

        _scintillaViewer = new ScintillaLogViewer(new ScintillaLogViewerOptions
        {
            AutoScroll = false,
            MaxLines = 300_000,
            MaxHistoryEntries = 300_000,
            DisableHistory = false,
            MaxBatchSize = 4096,
            FlushIntervalMs = 1,
            EnablePrettyJson = true,
            EnableTokenLinks = false,
            EnableTokenHighlight = false
        });
        ComboBenchmarkSupport.PrepareOffscreenControl(_scintillaViewer.HostControl);
        _scintillaPainter = new ComboBenchmarkSupport.OffscreenPainter(_scintillaViewer.ScintillaControl.ClientSize.Width, _scintillaViewer.ScintillaControl.ClientSize.Height);
        _scintillaViewer.Controller.Start();

        _zloggerScintillaFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.Services.AddSingleton<ILogViewerControlEvents, LogViewerControlEvents>();
            builder.Services.AddSingleton<IScintillaLogViewHost>(_scintillaViewer);
            builder.AddZLoggerScintilla();
        });
        _zloggerScintillaLogger = _zloggerScintillaFactory.CreateLogger("Bench.ZLogger.Scintilla.Pixel");

        var richSeed = new StringBuilder(_messages.Count * 64);
        for (var i = 0; i < _messages.Count; i++)
        {
            _serilogRichTextLogger.Information("{Message}", _messages[i]);
            _zloggerScintillaLogger.ZLogInformation($"{_messages[i]}");
            richSeed.AppendLine(_messages[i]);
        }

        ComboBenchmarkSupport.WaitForRichTextStable(_richTextBox, timeoutMs: 4000);
        ComboBenchmarkSupport.WaitForScintillaDrain(_scintillaViewer.Controller, _messages.Count, timeoutMs: 4000);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scintillaViewer.Controller.Stop();
        _scintillaPainter.Dispose();
        _richTextPainter.Dispose();
        _zloggerScintillaFactory.Dispose();
        _scintillaViewer.Dispose();
        (_serilogRichTextLogger as IDisposable)?.Dispose();
        _richTextBox.Dispose();
    }

    [Benchmark(Baseline = true, Description = "RichTextBox pixel draw only")]
    public int RichText_PixelDrawOnly()
    {
        return _richTextPainter.PaintAndHash(_richTextBox);
    }

    [Benchmark(Description = "Scintilla pixel draw only")]
    public int Scintilla_PixelDrawOnly()
    {
        return _scintillaPainter.PaintAndHash(_scintillaViewer.ScintillaControl);
    }
}
