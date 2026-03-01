using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Benchmarks.Benchmarking;
using RevitDevTool.Scintilla.Benchmarks.Scenarios;
using RevitDevTool.Scintilla.Control;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Extensions;
using Serilog;
using System.Windows.Forms;
using ZLogger;
using SerilogThemePresets = Serilog.Sinks.RichTextBoxForms.Themes.ThemePresets;

namespace RevitDevTool.Scintilla.Benchmarks.Benchmarks;

[Config(typeof(InProcessBenchmarkConfig))]
[MemoryDiagnoser]
public class ComboColorizedCoreBenchmarks
{
    private const int BatchSize = 1024;
    private readonly IReadOnlyList<string> _messages =
        ScenarioDataFactory.BuildMessages(BatchSize, 1024, TokenDensity.High, structuredPayload: true, seed: 202);

    private RichTextBox _richTextBox = null!;
    private Serilog.ILogger _serilogRichTextLogger = null!;

    private ScintillaLogViewer _scintillaViewer = null!;
    private Microsoft.Extensions.Logging.ILogger _zloggerScintillaLogger = null!;
    private Microsoft.Extensions.Logging.ILoggerFactory _zloggerScintillaFactory = null!;

    [GlobalSetup]
    public void Setup()
    {
        _richTextBox = new RichTextBox();
        ComboBenchmarkSupport.PrepareOffscreenControl(_richTextBox);
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
            MaxHistoryEntries = 0,
            DisableHistory = true,
            MaxBatchSize = 8192,
            FlushIntervalMs = 1,
            EnablePrettyJson = true,
            EnableTokenLinks = false,
            EnableTokenHighlight = false
        });
        ComboBenchmarkSupport.PrepareOffscreenControl(_scintillaViewer.HostControl);
        _scintillaViewer.Controller.Start();

        _zloggerScintillaFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.Services.AddSingleton<ILogViewerControlEvents, LogViewerControlEvents>();
            builder.Services.AddSingleton<IScintillaLogViewHost>(_scintillaViewer);
            builder.AddZLoggerScintilla();
        });
        _zloggerScintillaLogger = _zloggerScintillaFactory.CreateLogger("Bench.ZLogger.Scintilla.Colorized.Core");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _richTextBox.Clear();
        _scintillaViewer.Controller.Clear(ClearMode.Fast);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scintillaViewer.Controller.Stop();
        _zloggerScintillaFactory.Dispose();
        _scintillaViewer.Dispose();
        (_serilogRichTextLogger as IDisposable)?.Dispose();
        _richTextBox.Dispose();
    }

    [Benchmark(Baseline = true, Description = "Serilog + RichTextBox colorized core")]
    public int SerilogRichText_ColorizedCore()
    {
        for (var i = 0; i < _messages.Count; i++)
            _serilogRichTextLogger.Information("{Message}", _messages[i]);

        ComboBenchmarkSupport.WaitForRichTextStable(_richTextBox);
        return _richTextBox.TextLength;
    }

    [Benchmark(Description = "ZLogger + Scintilla colorized core")]
    public int ZLoggerScintilla_ColorizedCore()
    {
        for (var i = 0; i < _messages.Count; i++)
            _zloggerScintillaLogger.ZLogInformation($"{_messages[i]}");

        ComboBenchmarkSupport.WaitForScintillaDrain(_scintillaViewer.Controller, _messages.Count);
        return _scintillaViewer.ScintillaControl.TextLength;
    }
}
