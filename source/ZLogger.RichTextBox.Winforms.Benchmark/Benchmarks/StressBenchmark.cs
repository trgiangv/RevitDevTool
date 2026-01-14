using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ZLogger.RichTextBox.Winforms.Benchmark;

/// <summary>
/// High-volume stress test simulating real-world heavy logging scenarios.
/// Tests concurrent logging, sustained throughput, and buffer overflow behavior.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 1, iterationCount: 3)]
[MemoryDiagnoser]
[RankColumn]
public class StressBenchmark : RichTextBoxBenchmarkBase
{
    [Params(10000, 50000)]
    public int TotalMessages { get; set; }

    [GlobalSetup]
    public override void Setup()
    {
        MaxLogLines = 1000; // Fixed buffer size for stress test
        base.Setup();
    }

    [GlobalCleanup]
    public override void Cleanup()
    {
        base.Cleanup();
    }

    [Benchmark(Baseline = true, Description = "Serilog Sequential Stress")]
    public void Serilog_SequentialStress()
    {
        for (var i = 0; i < TotalMessages; i++)
        {
            var level = i % 6;
            switch (level)
            {
                case 0: SerilogLogger.Verbose("Stress test {Index} - Verbose", i); break;
                case 1: SerilogLogger.Debug("Stress test {Index} - Debug", i); break;
                case 2: SerilogLogger.Information("Stress test {Index} - Info", i); break;
                case 3: SerilogLogger.Warning("Stress test {Index} - Warning", i); break;
                case 4: SerilogLogger.Error("Stress test {Index} - Error", i); break;
                case 5: SerilogLogger.Fatal("Stress test {Index} - Critical", i); break;
            }
        }
    }

    [Benchmark(Description = "ZLogger Sequential Stress")]
    public void ZLogger_SequentialStress()
    {
        for (var i = 0; i < TotalMessages; i++)
        {
            var level = i % 6;
            switch (level)
            {
                case 0: ZLoggerLogger.LogTrace("Stress test {Index} - Trace", i); break;
                case 1: ZLoggerLogger.LogDebug("Stress test {Index} - Debug", i); break;
                case 2: ZLoggerLogger.LogInformation("Stress test {Index} - Info", i); break;
                case 3: ZLoggerLogger.LogWarning("Stress test {Index} - Warning", i); break;
                case 4: ZLoggerLogger.LogError("Stress test {Index} - Error", i); break;
                case 5: ZLoggerLogger.LogCritical("Stress test {Index} - Critical", i); break;
            }
        }
    }

    [Benchmark(Description = "Serilog Parallel Stress")]
    public void Serilog_ParallelStress()
    {
        Parallel.For(0, TotalMessages, i =>
        {
            SerilogLogger.Information("Parallel stress {ThreadId} - {Index}",
                Environment.CurrentManagedThreadId, i);
        });
    }

    [Benchmark(Description = "ZLogger Parallel Stress")]
    public void ZLogger_ParallelStress()
    {
        Parallel.For(0, TotalMessages, i =>
        {
            ZLoggerLogger.LogInformation("Parallel stress {ThreadId} - {Index}",
                Environment.CurrentManagedThreadId, i);
        });
    }

    [Benchmark(Description = "Serilog Task Stress")]
    public async Task Serilog_TaskStress()
    {
        var tasks = new List<Task>();
        var perTask = TotalMessages / 100;

        for (var t = 0; t < 100; t++)
        {
            var taskId = t;
            tasks.Add(Task.Run(() =>
            {
                for (var i = 0; i < perTask; i++)
                {
                    SerilogLogger.Information("Task {TaskId} message {Index}", taskId, i);
                }
            }));
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark(Description = "ZLogger Task Stress")]
    public async Task ZLogger_TaskStress()
    {
        var tasks = new List<Task>();
        var perTask = TotalMessages / 100;

        for (var t = 0; t < 100; t++)
        {
            var taskId = t;
            tasks.Add(Task.Run(() =>
            {
                for (var i = 0; i < perTask; i++)
                {
                    ZLoggerLogger.LogInformation("Task {TaskId} message {Index}", taskId, i);
                }
            }));
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark(Description = "Serilog Mixed Workload")]
    public void Serilog_MixedWorkload()
    {
        var exception = new InvalidOperationException("Test", new ArgumentException("Inner"));
        var complexData = new
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Values = new[] { 1, 2, 3, 4, 5 },
            Nested = new { A = "value", B = 42 }
        };

        for (var i = 0; i < TotalMessages; i++)
        {
            var type = i % 4;
            switch (type)
            {
                case 0:
                    SerilogLogger.Information("Simple message {Index}", i);
                    break;
                case 1:
                    SerilogLogger.Warning("Warning with data: {@Data}", complexData);
                    break;
                case 2:
                    SerilogLogger.Error(exception, "Error at index {Index}", i);
                    break;
                case 3:
                    SerilogLogger.Debug("Debug: {A}, {B}, {C}, {D}, {E}", i, i * 2, i * 3, i * 4, i * 5);
                    break;
            }
        }
    }

    [Benchmark(Description = "ZLogger Mixed Workload")]
    public void ZLogger_MixedWorkload()
    {
        var exception = new InvalidOperationException("Test", new ArgumentException("Inner"));
        var complexData = new
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Values = new[] { 1, 2, 3, 4, 5 },
            Nested = new { A = "value", B = 42 }
        };

        for (var i = 0; i < TotalMessages; i++)
        {
            var type = i % 4;
            switch (type)
            {
                case 0:
                    ZLoggerLogger.LogInformation("Simple message {Index}", i);
                    break;
                case 1:
                    ZLoggerLogger.LogWarning("Warning with data: {Data}", complexData);
                    break;
                case 2:
                    ZLoggerLogger.LogError(exception, "Error at index {Index}", i);
                    break;
                case 3:
                    ZLoggerLogger.LogDebug("Debug: {A}, {B}, {C}, {D}, {E}", i, i * 2, i * 3, i * 4, i * 5);
                    break;
            }
        }
    }
}
