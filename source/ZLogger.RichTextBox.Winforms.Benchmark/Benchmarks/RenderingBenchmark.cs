using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace ZLogger.RichTextBox.Winforms.Benchmark;

/// <summary>
/// RTF rendering benchmark - tests the UI rendering path specifically.
/// Measures how fast each framework can render RTF to the RichTextBox.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[RankColumn]
public class RenderingBenchmark : RichTextBoxBenchmarkBase
{
    [Params(100, 500, 1000)]
    public int RenderCount { get; set; }

    [GlobalSetup]
    public override void Setup()
    {
        MaxLogLines = RenderCount; // Match buffer size to render count
        base.Setup();
    }

    [GlobalCleanup]
    public override void Cleanup()
    {
        base.Cleanup();
    }

    [Benchmark(Baseline = true, Description = "Serilog RTF Render Cycle")]
    public void Serilog_RenderCycle()
    {
        // Fill buffer
        for (var i = 0; i < RenderCount; i++)
        {
            SerilogLogger.Information("[{Level:u3}] Message {Index}: Processing data with timestamp {Time}",
                "INF", i, DateTime.UtcNow);
        }

        // Wait for rendering to complete
        Thread.Sleep(100);

        // Clear and refill (simulates continuous logging)
        SerilogSink?.Clear();

        for (var i = 0; i < RenderCount; i++)
        {
            SerilogLogger.Warning("Warning {Index}: Resource usage at {Percent}%", i, i % 100);
        }

        Thread.Sleep(100);
    }

    [Benchmark(Description = "ZLogger RTF Render Cycle")]
    public void ZLogger_RenderCycle()
    {
        // Fill buffer
        for (var i = 0; i < RenderCount; i++)
        {
            ZLoggerLogger.LogInformation("[{Level}] Message {Index}: Processing data with timestamp {Time}",
                "INF", i, DateTime.UtcNow);
        }

        // Wait for rendering to complete
        Thread.Sleep(100);

        // Clear and refill
        ZLoggerProvider?.Processor.Clear();

        for (var i = 0; i < RenderCount; i++)
        {
            ZLoggerLogger.LogWarning("Warning {Index}: Resource usage at {Percent}%", i, i % 100);
        }

        Thread.Sleep(100);
    }

    [Benchmark(Description = "Serilog Unicode Content")]
    public void Serilog_UnicodeContent()
    {
        var unicodeMessages = new[]
        {
            "日本語テスト: {Index}",
            "Ελληνικά δοκιμή: {Index}",
            "Тест кириллицы: {Index}",
            "العربية اختبار: {Index}",
            "🚀 Emoji test: {Index}",
            "Mixed: ABC αβγ 中文 🎉 - {Index}"
        };

        for (var i = 0; i < RenderCount; i++)
        {
            var msg = unicodeMessages[i % unicodeMessages.Length];
            SerilogLogger.Information(msg, i);
        }

        Thread.Sleep(100);
    }

    [Benchmark(Description = "ZLogger Unicode Content")]
    public void ZLogger_UnicodeContent()
    {
        var unicodeMessages = new[]
        {
            "日本語テスト: {Index}",
            "Ελληνικά δοκιμή: {Index}",
            "Тест кириллицы: {Index}",
            "العربية اختبار: {Index}",
            "🚀 Emoji test: {Index}",
            "Mixed: ABC αβγ 中文 🎉 - {Index}"
        };

        for (var i = 0; i < RenderCount; i++)
        {
            var msg = unicodeMessages[i % unicodeMessages.Length];
            ZLoggerLogger.LogInformation(msg, i);
        }

        Thread.Sleep(100);
    }

    [Benchmark(Description = "Serilog Long Lines")]
    public void Serilog_LongLines()
    {
        var longLine = new string('A', 500);

        for (var i = 0; i < RenderCount; i++)
        {
            SerilogLogger.Information("Long line {Index}: {Content}", i, longLine);
        }

        Thread.Sleep(100);
    }

    [Benchmark(Description = "ZLogger Long Lines")]
    public void ZLogger_LongLines()
    {
        var longLine = new string('A', 500);

        for (var i = 0; i < RenderCount; i++)
        {
            ZLoggerLogger.LogInformation("Long line {Index}: {Content}", i, longLine);
        }

        Thread.Sleep(100);
    }

    [Benchmark(Description = "Serilog Color Switching")]
    public void Serilog_ColorSwitching()
    {
        // Rapid level changes = rapid color changes in RTF
        for (var i = 0; i < RenderCount; i++)
        {
            switch (i % 6)
            {
                case 0: SerilogLogger.Verbose("V{Index}", i); break;
                case 1: SerilogLogger.Debug("D{Index}", i); break;
                case 2: SerilogLogger.Information("I{Index}", i); break;
                case 3: SerilogLogger.Warning("W{Index}", i); break;
                case 4: SerilogLogger.Error("E{Index}", i); break;
                case 5: SerilogLogger.Fatal("F{Index}", i); break;
            }
        }

        Thread.Sleep(100);
    }

    [Benchmark(Description = "ZLogger Color Switching")]
    public void ZLogger_ColorSwitching()
    {
        for (var i = 0; i < RenderCount; i++)
        {
            switch (i % 6)
            {
                case 0: ZLoggerLogger.LogTrace("V{Index}", i); break;
                case 1: ZLoggerLogger.LogDebug("D{Index}", i); break;
                case 2: ZLoggerLogger.LogInformation("I{Index}", i); break;
                case 3: ZLoggerLogger.LogWarning("W{Index}", i); break;
                case 4: ZLoggerLogger.LogError("E{Index}", i); break;
                case 5: ZLoggerLogger.LogCritical("F{Index}", i); break;
            }
        }

        Thread.Sleep(100);
    }
}
