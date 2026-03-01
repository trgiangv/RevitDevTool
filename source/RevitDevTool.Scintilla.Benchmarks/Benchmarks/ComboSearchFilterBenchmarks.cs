using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Benchmarks.Benchmarking;
using RevitDevTool.Scintilla.Benchmarks.Scenarios;
using RevitDevTool.Scintilla.Control;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Logger;
using RevitDevTool.Scintilla.Search;
using Serilog;
using SerilogThemePresets = Serilog.Sinks.RichTextBoxForms.Themes.ThemePresets;
using System.Text;
using System.Windows.Forms;

namespace RevitDevTool.Scintilla.Benchmarks.Benchmarks;

[Config(typeof(InProcessBenchmarkConfig))]
[MemoryDiagnoser]
public class ComboSearchFilterBenchmarks
{
    private readonly IReadOnlyList<string> _messages =
        ScenarioDataFactory.BuildMessages(50_000, 256, TokenDensity.High, structuredPayload: false);

    private RichTextBox _richTextBox = null!;
    private Serilog.ILogger _serilogRichTextLogger = null!;
    private string _richTextSnapshot = string.Empty;
    private ComboBenchmarkSupport.OffscreenPainter _richTextPainter = null!;
    private ScintillaLogViewer _scintillaViewer = null!;
    private ILogEntrySink _scintillaSink = null!;
    private ComboBenchmarkSupport.OffscreenPainter _scintillaPainter = null!;

    [GlobalSetup]
    public void Setup()
    {
        _richTextBox = new RichTextBox();
        ComboBenchmarkSupport.PrepareOffscreenControl(_richTextBox);
        _richTextPainter = new ComboBenchmarkSupport.OffscreenPainter(_richTextBox.ClientSize.Width, _richTextBox.ClientSize.Height);
        var sb = new StringBuilder(_messages.Count * 64);
        for (var i = 0; i < _messages.Count; i++)
            sb.AppendLine(_messages[i]);
        _richTextSnapshot = sb.ToString();
        _richTextBox.Text = _richTextSnapshot;
        _serilogRichTextLogger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.RichTextBox(
                _richTextBox,
                theme: SerilogThemePresets.EnhancedDark,
                autoScroll: false,
                maxLogLines: 300_000,
                prettyPrintJson: false,
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
            EnablePrettyJson = false,
            EnableTokenLinks = false,
            EnableTokenHighlight = false
        });
        ComboBenchmarkSupport.PrepareOffscreenControl(_scintillaViewer.HostControl);
        _scintillaPainter = new ComboBenchmarkSupport.OffscreenPainter(_scintillaViewer.ScintillaControl.ClientSize.Width, _scintillaViewer.ScintillaControl.ClientSize.Height);
        _scintillaViewer.Controller.Start();
        _scintillaSink = (ILogEntrySink)_scintillaViewer.Controller;

        for (var i = 0; i < _messages.Count; i++)
        {
            var bytes = Encoding.UTF8.GetBytes(_messages[i]);
            _scintillaSink.TryPost(new LogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Level = LogLevel.Information,
                Source = "SearchFilterBench",
                Message = new ArraySegment<byte>(bytes, 0, bytes.Length),
                Properties = LogEntry.EmptyProperties
            });
        }
        ComboBenchmarkSupport.WaitForScintillaDrain(_scintillaViewer.Controller, _messages.Count, timeoutMs: 5000);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scintillaViewer.Controller.Stop();
        _scintillaPainter.Dispose();
        _richTextPainter.Dispose();
        _scintillaViewer.Dispose();
        (_serilogRichTextLogger as IDisposable)?.Dispose();
        _richTextBox.Dispose();
    }

    [Benchmark(Baseline = true, Description = "RichTextBox filter contains")]
    public int RichText_FilterContains()
    {
        _richTextBox.Clear();
        var lines = _richTextSnapshot.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var count = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].IndexOf("ORD-12345", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                count++;
                _serilogRichTextLogger.Information("{Message}", lines[i]);
            }
        }

        ComboBenchmarkSupport.WaitForRichTextStable(_richTextBox);
        var pixelHash = _richTextPainter.PaintAndHash(_richTextBox);
        return count ^ pixelHash;
    }

    [Benchmark(Description = "Scintilla filter contains")]
    public int Scintilla_FilterContains()
    {
        _scintillaViewer.Controller.ApplyFilter(new LogFilterOptions
        {
            TextContains = "ORD-12345",
            MatchCase = false
        });
        var lineCount = WaitForScintillaLineCountStable();
        var pixelHash = _scintillaPainter.PaintAndHash(_scintillaViewer.ScintillaControl);
        return lineCount ^ pixelHash;
    }

    [Benchmark(Description = "RichTextBox search next")]
    public int RichText_SearchNext()
    {
        _richTextBox.Select(0, 0);
        var position = _richTextBox.Find("TOKEN_NOT_FOUND_987654321", RichTextBoxFinds.MatchCase);
        var pixelHash = _richTextPainter.PaintAndHash(_richTextBox);
        return position ^ pixelHash;
    }

    [Benchmark(Description = "Scintilla search next")]
    public int Scintilla_SearchNext()
    {
        _scintillaViewer.ScintillaControl.CurrentPosition = 0;
        var result = _scintillaViewer.Controller.FindNext("TOKEN_NOT_FOUND_987654321", matchCase: true, useRegex: false);
        var position = result.Found ? result.StartPosition : -1;
        var pixelHash = _scintillaPainter.PaintAndHash(_scintillaViewer.ScintillaControl);
        return position ^ pixelHash;
    }

    private int WaitForScintillaLineCountStable(int timeoutMs = 3000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var lastCount = -1;
        var stable = 0;
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var current = _scintillaViewer.ScintillaControl.Lines.Count;
            if (current == lastCount)
            {
                stable++;
                if (stable >= 2)
                    return current;
            }
            else
            {
                stable = 0;
                lastCount = current;
            }

            Thread.Sleep(1);
        }

        return _scintillaViewer.ScintillaControl.Lines.Count;
    }
}
