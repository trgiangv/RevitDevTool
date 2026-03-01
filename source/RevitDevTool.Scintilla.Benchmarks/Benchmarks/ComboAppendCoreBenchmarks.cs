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

namespace RevitDevTool.Scintilla.Benchmarks.Benchmarks;

[Config(typeof(InProcessBenchmarkConfig))]
[MemoryDiagnoser]
public class ComboAppendCoreBenchmarks
{
    private const int BatchSize = 2000;
    private readonly IReadOnlyList<string> _messages =
        ScenarioDataFactory.BuildMessages(BatchSize, 256, TokenDensity.None, structuredPayload: false, seed: 101);

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
            .WriteTo.Sink(new ComboBenchmarkSupport.PlainRichTextBoxSerilogSink(_richTextBox))
            .CreateLogger();

        _scintillaViewer = new ScintillaLogViewer(new ScintillaLogViewerOptions
        {
            AutoScroll = false,
            MaxLines = 300_000,
            MaxHistoryEntries = 0,
            DisableHistory = true,
            MaxBatchSize = 4096,
            FlushIntervalMs = 1,
            EnablePrettyJson = false
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
        _zloggerScintillaLogger = _zloggerScintillaFactory.CreateLogger("Bench.ZLogger.Scintilla.Append.Core");
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

    [Benchmark(Baseline = true, Description = "Serilog + RichTextBox append core")]
    public int SerilogRichText_AppendCore()
    {
        for (var i = 0; i < _messages.Count; i++)
            _serilogRichTextLogger.Information("{Message}", _messages[i]);

        ComboBenchmarkSupport.WaitForRichTextLineCountAtLeast(_richTextBox, _messages.Count);
        return _richTextBox.TextLength;
    }

    [Benchmark(Description = "ZLogger + Scintilla append core")]
    public int ZLoggerScintilla_AppendCore()
    {
        for (var i = 0; i < _messages.Count; i++)
            _zloggerScintillaLogger.ZLogInformation($"{_messages[i]}");

        ComboBenchmarkSupport.WaitForScintillaDrain(_scintillaViewer.Controller, _messages.Count);
        return _scintillaViewer.ScintillaControl.TextLength;
    }
}
