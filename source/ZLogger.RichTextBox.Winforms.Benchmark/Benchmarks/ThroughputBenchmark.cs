using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging;

namespace ZLogger.RichTextBox.Winforms.Benchmark;

/// <summary>
/// Measures raw logging throughput - how many log messages can be processed per second.
/// This tests the core logging pipeline without UI rendering overhead.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[RankColumn]
public class ThroughputBenchmark : RichTextBoxBenchmarkBase
{
    [Params(100, 1000, 10000)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public override void Setup()
    {
        base.Setup();
    }

    [GlobalCleanup]
    public override void Cleanup()
    {
        base.Cleanup();
    }

    [Benchmark(Baseline = true, Description = "Serilog Simple Message")]
    public void Serilog_SimpleMessage()
    {
        for (var i = 0; i < MessageCount; i++)
        {
            SerilogLogger.Information("Processing item {ItemId} of {Total}", i, MessageCount);
        }
    }

    [Benchmark(Description = "ZLogger Simple Message")]
    public void ZLogger_SimpleMessage()
    {
        for (var i = 0; i < MessageCount; i++)
        {
            ZLoggerLogger.LogInformation("Processing item {ItemId} of {Total}", i, MessageCount);
        }
    }

    [Benchmark(Description = "Serilog With Exception")]
    public void Serilog_WithException()
    {
        var exception = new InvalidOperationException("Test exception", new ArgumentException("Inner"));
        for (var i = 0; i < MessageCount; i++)
        {
            SerilogLogger.Error(exception, "Error processing item {ItemId}", i);
        }
    }

    [Benchmark(Description = "ZLogger With Exception")]
    public void ZLogger_WithException()
    {
        var exception = new InvalidOperationException("Test exception", new ArgumentException("Inner"));
        for (var i = 0; i < MessageCount; i++)
        {
            ZLoggerLogger.LogError(exception, "Error processing item {ItemId}", i);
        }
    }

    [Benchmark(Description = "Serilog Complex Object")]
    public void Serilog_ComplexObject()
    {
        var data = new
        {
            UserId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Metrics = new { ResponseTime = 45.5, ErrorRate = 0.01, Requests = 1500 },
            Tags = new[] { "production", "api", "critical" }
        };

        for (var i = 0; i < MessageCount; i++)
        {
            SerilogLogger.Information("Request completed: {@Data}", data);
        }
    }

    [Benchmark(Description = "ZLogger Complex Object")]
    public void ZLogger_ComplexObject()
    {
        var data = new
        {
            UserId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Metrics = new { ResponseTime = 45.5, ErrorRate = 0.01, Requests = 1500 },
            Tags = new[] { "production", "api", "critical" }
        };

        for (var i = 0; i < MessageCount; i++)
        {
            ZLoggerLogger.LogInformation("Request completed: {Data}", data);
        }
    }

    [Benchmark(Description = "Serilog All Levels")]
    public void Serilog_AllLevels()
    {
        var perLevel = MessageCount / 6;
        for (var i = 0; i < perLevel; i++)
        {
            SerilogLogger.Verbose("Trace message {Index}", i);
            SerilogLogger.Debug("Debug message {Index}", i);
            SerilogLogger.Information("Info message {Index}", i);
            SerilogLogger.Warning("Warning message {Index}", i);
            SerilogLogger.Error("Error message {Index}", i);
            SerilogLogger.Fatal("Critical message {Index}", i);
        }
    }

    [Benchmark(Description = "ZLogger All Levels")]
    public void ZLogger_AllLevels()
    {
        var perLevel = MessageCount / 6;
        for (var i = 0; i < perLevel; i++)
        {
            ZLoggerLogger.LogTrace("Trace message {Index}", i);
            ZLoggerLogger.LogDebug("Debug message {Index}", i);
            ZLoggerLogger.LogInformation("Info message {Index}", i);
            ZLoggerLogger.LogWarning("Warning message {Index}", i);
            ZLoggerLogger.LogError("Error message {Index}", i);
            ZLoggerLogger.LogCritical("Critical message {Index}", i);
        }
    }
}
