using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.RichTextBox.Winforms;
using ZLogger.RichTextBox.Winforms.Themes;
using WinFormsRichTextBox = System.Windows.Forms.RichTextBox;

namespace ZLogger.RichTextBox.Winforms.Benchmark;

/// <summary>
/// Isolates the memory issue - compares:
/// 1. ZLogger Core (no sink) - measures ZLogger's internal allocation
/// 2. ZLogger + RichTextBox - measures full pipeline including RTF rendering
/// 
/// This helps identify if the memory issue is in ZLogger core or RichTextBox rendering.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[RankColumn]
public class CoreVsRenderingBenchmark : IDisposable
{
    private const int Iterations = 1000;
    private string _largeString = null!;

    // ZLogger Core only (NullLogger - no output)
    private ILoggerFactory? _coreOnlyFactory;
    private ILogger? _coreOnlyLogger;

    // ZLogger + RichTextBox (full pipeline)
    private WinFormsRichTextBox? _richTextBox;
    private ILoggerFactory? _rtbFactory;
    private ILogger? _rtbLogger;
    private ZLoggerRichTextBoxLoggerProvider? _rtbProvider;

    // ZLogger + Stream (writes to MemoryStream, no RTF)
    private MemoryStream? _memoryStream;
    private ILoggerFactory? _streamFactory;
    private ILogger? _streamLogger;

    [GlobalSetup]
    public void Setup()
    {
        _largeString = new string('X', 1000);

        // Setup 1: ZLogger Core only - NullLoggerProvider (no output)
        _coreOnlyFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddZLoggerStream(Stream.Null); // Writes to nowhere
        });
        _coreOnlyLogger = _coreOnlyFactory.CreateLogger("CoreOnly");

        // Setup 2: ZLogger + RichTextBox (full RTF rendering)
        _richTextBox = new WinFormsRichTextBox
        {
            Font = new Font("Cascadia Mono", 9f),
            ReadOnly = true
        };
        _rtbFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddZLoggerRichTextBox(_richTextBox, out var provider, options =>
            {
                options.Theme = ThemePresets.Literate;
                options.MaxLogLines = 1000;
            });
            _rtbProvider = provider;
        });
        _rtbLogger = _rtbFactory.CreateLogger("RichTextBox");

        // Setup 3: ZLogger + MemoryStream (UTF-8 output, no RTF)
        _memoryStream = new MemoryStream();
        _streamFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddZLoggerStream(_memoryStream);
        });
        _streamLogger = _streamFactory.CreateLogger("Stream");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _coreOnlyFactory?.Dispose();
        _rtbProvider?.Dispose();
        _rtbFactory?.Dispose();
        _richTextBox?.Dispose();
        _streamFactory?.Dispose();
        _memoryStream?.Dispose();
    }

    // ==================== Large String Tests ====================

    [Benchmark(Baseline = true, Description = "ZLogger Core (Stream.Null) - Large String")]
    public void ZLoggerCore_LargeString()
    {
        for (var i = 0; i < Iterations; i++)
        {
            _coreOnlyLogger!.LogInformation("Large payload: {Payload}", _largeString);
        }
    }

    [Benchmark(Description = "ZLogger + MemoryStream - Large String")]
    public void ZLoggerStream_LargeString()
    {
        _memoryStream!.SetLength(0); // Reset
        for (var i = 0; i < Iterations; i++)
        {
            _streamLogger!.LogInformation("Large payload: {Payload}", _largeString);
        }
    }

    [Benchmark(Description = "ZLogger + RichTextBox - Large String")]
    public void ZLoggerRtb_LargeString()
    {
        for (var i = 0; i < Iterations; i++)
        {
            _rtbLogger!.LogInformation("Large payload: {Payload}", _largeString);
        }
    }

    // ==================== String Interpolation Tests ====================

    [Benchmark(Description = "ZLogger Core (Stream.Null) - String Interpolation")]
    public void ZLoggerCore_StringInterpolation()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var id = i;
            var name = $"User_{i}";
            var timestamp = DateTime.UtcNow;
            _coreOnlyLogger!.LogInformation("User {Id} ({Name}) logged in at {Timestamp}", id, name, timestamp);
        }
    }

    [Benchmark(Description = "ZLogger + RichTextBox - String Interpolation")]
    public void ZLoggerRtb_StringInterpolation()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var id = i;
            var name = $"User_{i}";
            var timestamp = DateTime.UtcNow;
            _rtbLogger!.LogInformation("User {Id} ({Name}) logged in at {Timestamp}", id, name, timestamp);
        }
    }

    // ==================== Dictionary Tests ====================

    [Benchmark(Description = "ZLogger Core (Stream.Null) - Dictionary")]
    public void ZLoggerCore_Dictionary()
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
            _coreOnlyLogger!.LogInformation("Data: {Data}", data);
        }
    }

    [Benchmark(Description = "ZLogger + RichTextBox - Dictionary")]
    public void ZLoggerRtb_Dictionary()
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
            _rtbLogger!.LogInformation("Data: {Data}", data);
        }
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}
