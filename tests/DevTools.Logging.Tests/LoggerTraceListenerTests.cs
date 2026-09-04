using System.Diagnostics;
using DevTools.Logging;
using DevTools.Logging.Listeners;
using DevTools.Logging.Options;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Logging.Tests;

public sealed class LoggerTraceListenerTests
{
    [Fact]
    public void LoggerTraceListener_routes_trace_output_to_zlogger()
    {
        var delivered = new List<LogLevel>();
        using var factory = LoggerFactory.Create(static builder => builder.AddZLoggerConsole());
        var logger = factory.CreateLogger("trace");
        var options = new TraceListenerOptions
        {
            IncludeStackTrace = true,
            StackTraceDepth = 2,
            LevelKeys = new LogLevelKeys
            {
                Critical = "fatal",
                Error = "failed",
                Warning = "slow",
                Information = "started",
            },
        };

        var listener = new LoggerTraceListener(logger, options, delivered.Add)
        {
            Filter = new EventTypeFilter(SourceLevels.All),
        };

        listener.Fail("boom");
        listener.Fail("a", "b");
        listener.Write("[ERROR] disk");
        listener.WriteLine("[WARN] slow");
        listener.Write("plain", "cat");
        listener.WriteLine("obj", "cat");
        listener.Write(42);
        listener.WriteLine(42);
        listener.Write("started", "cat");
        listener.WriteLine("started", "cat");

        var cache = new TraceEventCache();
        listener.TraceEvent(cache, "src", TraceEventType.Critical, 1);
        listener.TraceEvent(cache, "src", TraceEventType.Error, 2, "err");
        listener.TraceEvent(cache, "src", TraceEventType.Information, 3, "fmt {0}", "x");
        listener.TraceData(cache, "src", TraceEventType.Warning, 4, "data");
        listener.TraceData(cache, "src", TraceEventType.Verbose, 5, "a", "b");
        listener.TraceTransfer(cache, "src", 6, "xfer", Guid.NewGuid());

        Assert.NotEmpty(delivered);
        Assert.Contains(LogLevel.Critical, delivered);
        Assert.Contains(LogLevel.Error, delivered);
        Assert.Contains(LogLevel.Warning, delivered);
        Assert.Contains(LogLevel.Information, delivered);
    }

    [Fact]
    public void LoggerTraceListener_callback_exceptions_are_swallowed()
    {
        using var factory = LoggerFactory.Create(static builder => builder.AddZLoggerConsole());
        var listener = new LoggerTraceListener(
            factory.CreateLogger("trace"),
            new TraceListenerOptions(),
            _ => throw new InvalidOperationException("boom"));
        var ex = Record.Exception(() => listener.Write("hello"));
        Assert.Null(ex);
    }

    [Fact]
    public void LoggerTraceListener_ctor_rejects_null_dependencies()
    {
        using var factory = LoggerFactory.Create(static _ => { });
        var logger = factory.CreateLogger("x");
        Assert.Throws<ArgumentNullException>(() => new LoggerTraceListener(null!, new TraceListenerOptions()));
        Assert.Throws<ArgumentNullException>(() => new LoggerTraceListener(logger, null!));
    }
}
