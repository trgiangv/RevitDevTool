using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging;

namespace ZLogger.RichTextBox.Winforms.Benchmark;

/// <summary>
/// Memory allocation benchmark - measures heap allocations per log operation.
/// Critical for high-volume logging scenarios to avoid GC pressure.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[RankColumn]
public class MemoryBenchmark : RichTextBoxBenchmarkBase
{
    private const int Iterations = 1000;

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

    [Benchmark(Baseline = true, Description = "Serilog - String Interpolation")]
    public void Serilog_StringInterpolation()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var id = i;
            var name = $"User_{i}";
            var timestamp = DateTime.UtcNow;
            SerilogLogger.Information("User {Id} ({Name}) logged in at {Timestamp}", id, name, timestamp);
        }
    }

    [Benchmark(Description = "ZLogger - String Interpolation")]
    public void ZLogger_StringInterpolation()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var id = i;
            var name = $"User_{i}";
            var timestamp = DateTime.UtcNow;
            ZLoggerLogger.LogInformation("User {Id} ({Name}) logged in at {Timestamp}", id, name, timestamp);
        }
    }

    [Benchmark(Description = "Serilog - Value Types Only")]
    public void Serilog_ValueTypesOnly()
    {
        for (var i = 0; i < Iterations; i++)
        {
            SerilogLogger.Information("Processing: Count={Count}, Duration={Duration}ms, Success={Success}",
                i, 123.45, true);
        }
    }

    [Benchmark(Description = "ZLogger - Value Types Only")]
    public void ZLogger_ValueTypesOnly()
    {
        for (var i = 0; i < Iterations; i++)
        {
            ZLoggerLogger.LogInformation("Processing: Count={Count}, Duration={Duration}ms, Success={Success}",
                i, 123.45, true);
        }
    }

    [Benchmark(Description = "Serilog - Large String")]
    public void Serilog_LargeString()
    {
        var largeString = new string('X', 1000);
        for (var i = 0; i < Iterations; i++)
        {
            SerilogLogger.Information("Large payload: {Payload}", largeString);
        }
    }

    [Benchmark(Description = "ZLogger - Large String")]
    public void ZLogger_LargeString()
    {
        var largeString = new string('X', 1000);
        for (var i = 0; i < Iterations; i++)
        {
            ZLoggerLogger.LogInformation("Large payload: {Payload}", largeString);
        }
    }

    [Benchmark(Description = "Serilog - Dictionary")]
    public void Serilog_Dictionary()
    {
        var data = new Dictionary<string, object>
        {
            ["Key1"] = "Value1",
            ["Key2"] = 42,
            ["Key3"] = true,
            ["Key4"] = DateTime.UtcNow
        };

        for (var i = 0; i < Iterations; i++)
        {
            SerilogLogger.Information("Data: {@Data}", data);
        }
    }

    [Benchmark(Description = "ZLogger - Dictionary")]
    public void ZLogger_Dictionary()
    {
        var data = new Dictionary<string, object>
        {
            ["Key1"] = "Value1",
            ["Key2"] = 42,
            ["Key3"] = true,
            ["Key4"] = DateTime.UtcNow
        };

        for (var i = 0; i < Iterations; i++)
        {
            ZLoggerLogger.LogInformation("Data: {Data}", data);
        }
    }

    [Benchmark(Description = "Serilog - Nested Exception")]
    public void Serilog_NestedException()
    {
        var inner2 = new ArgumentNullException("param", "Inner inner exception");
        var inner1 = new InvalidOperationException("Inner exception", inner2);
        var exception = new ApplicationException("Outer exception", inner1);

        for (var i = 0; i < Iterations; i++)
        {
            SerilogLogger.Error(exception, "Operation failed at step {Step}", i);
        }
    }

    [Benchmark(Description = "ZLogger - Nested Exception")]
    public void ZLogger_NestedException()
    {
        var inner2 = new ArgumentNullException("param", "Inner inner exception");
        var inner1 = new InvalidOperationException("Inner exception", inner2);
        var exception = new ApplicationException("Outer exception", inner1);

        for (var i = 0; i < Iterations; i++)
        {
            ZLoggerLogger.LogError(exception, "Operation failed at step {Step}", i);
        }
    }
}
