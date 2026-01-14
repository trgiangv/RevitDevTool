using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ZLogger.RichTextBox.Winforms.Benchmark;

/// <summary>
/// Measures end-to-end latency from log call to buffer write.
/// Uses manual timing to capture actual per-operation latency.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[RankColumn]
public class LatencyBenchmark : RichTextBoxBenchmarkBase
{
    private readonly List<long> _serilogLatencies = new(10000);
    private readonly List<long> _zloggerLatencies = new(10000);

    [Params(100, 1000)]
    public int SampleCount { get; set; }

    [GlobalSetup]
    public override void Setup()
    {
        base.Setup();
        _serilogLatencies.Clear();
        _zloggerLatencies.Clear();
    }

    [GlobalCleanup]
    public override void Cleanup()
    {
        PrintLatencyStatistics();
        base.Cleanup();
    }

    [Benchmark(Baseline = true, Description = "Serilog Latency")]
    public void Serilog_Latency()
    {
        _serilogLatencies.Clear();
        var sw = new Stopwatch();

        for (var i = 0; i < SampleCount; i++)
        {
            sw.Restart();
            SerilogLogger.Information("Latency test message {Index} with data {Data}", i, Guid.NewGuid());
            sw.Stop();
            _serilogLatencies.Add(sw.ElapsedTicks);
        }
    }

    [Benchmark(Description = "ZLogger Latency")]
    public void ZLogger_Latency()
    {
        _zloggerLatencies.Clear();
        var sw = new Stopwatch();

        for (var i = 0; i < SampleCount; i++)
        {
            sw.Restart();
            ZLoggerLogger.LogInformation("Latency test message {Index} with data {Data}", i, Guid.NewGuid());
            sw.Stop();
            _zloggerLatencies.Add(sw.ElapsedTicks);
        }
    }

    [Benchmark(Description = "Serilog Burst Latency")]
    public void Serilog_BurstLatency()
    {
        // Simulate burst logging - 10 messages at once
        var sw = new Stopwatch();

        for (var burst = 0; burst < SampleCount / 10; burst++)
        {
            sw.Restart();
            for (var i = 0; i < 10; i++)
            {
                SerilogLogger.Information("Burst {Burst} message {Index}", burst, i);
            }
            sw.Stop();
            _serilogLatencies.Add(sw.ElapsedTicks);
        }
    }

    [Benchmark(Description = "ZLogger Burst Latency")]
    public void ZLogger_BurstLatency()
    {
        var sw = new Stopwatch();

        for (var burst = 0; burst < SampleCount / 10; burst++)
        {
            sw.Restart();
            for (var i = 0; i < 10; i++)
            {
                ZLoggerLogger.LogInformation("Burst {Burst} message {Index}", burst, i);
            }
            sw.Stop();
            _zloggerLatencies.Add(sw.ElapsedTicks);
        }
    }

    private void PrintLatencyStatistics()
    {
        if (_serilogLatencies.Count > 0)
        {
            Console.WriteLine("\n=== Serilog Latency Statistics ===");
            PrintStats(_serilogLatencies);
        }

        if (_zloggerLatencies.Count > 0)
        {
            Console.WriteLine("\n=== ZLogger Latency Statistics ===");
            PrintStats(_zloggerLatencies);
        }
    }

    private static void PrintStats(List<long> ticks)
    {
        var sorted = ticks.OrderBy(x => x).ToList();
        var ticksPerMicrosecond = Stopwatch.Frequency / 1_000_000.0;

        var min = sorted[0] / ticksPerMicrosecond;
        var max = sorted[^1] / ticksPerMicrosecond;
        var avg = sorted.Average() / ticksPerMicrosecond;
        var median = sorted[sorted.Count / 2] / ticksPerMicrosecond;
        var p95 = sorted[(int)(sorted.Count * 0.95)] / ticksPerMicrosecond;
        var p99 = sorted[(int)(sorted.Count * 0.99)] / ticksPerMicrosecond;

        Console.WriteLine($"  Min:    {min:F2} µs");
        Console.WriteLine($"  Max:    {max:F2} µs");
        Console.WriteLine($"  Avg:    {avg:F2} µs");
        Console.WriteLine($"  Median: {median:F2} µs");
        Console.WriteLine($"  P95:    {p95:F2} µs");
        Console.WriteLine($"  P99:    {p99:F2} µs");
    }
}
