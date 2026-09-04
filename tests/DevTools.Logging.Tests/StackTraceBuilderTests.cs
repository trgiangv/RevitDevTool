using System.Diagnostics;
using DevTools.Logging;
using DevTools.Logging.Listeners;
using DevTools.Logging.Options;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Logging.Tests;

public sealed class StackTraceBuilderTests
{
    [Fact]
    public void BuildStackTrace_returns_empty_when_depth_is_zero()
    {
        Assert.Equal(string.Empty, StackTraceBuilder.BuildStackTrace(null, 0));
        Assert.Equal(string.Empty, StackTraceBuilder.BuildStackTrace(null, -1));
    }

    [Fact]
    public void BuildStackTrace_returns_empty_for_null_cache()
    {
        Assert.Equal(string.Empty, StackTraceBuilder.BuildStackTrace(null, 3));
    }

    [Fact]
    public void BuildStackTrace_formats_callstack_lines()
    {
        TraceEventCache? cache = null;
        var source = new TraceSource("stack-trace-test") { Switch = { Level = SourceLevels.All } };
        source.Listeners.Add(new CacheCapturingListener(c => cache = c)
        {
            TraceOutputOptions = TraceOptions.Callstack,
        });
        EmitTrace(source);

        Assert.NotNull(cache);
        var stack = StackTraceBuilder.BuildStackTrace(
            cache,
            maxDepth: 4,
            ignoredNamespacePrefixes: ["System."],
            ignoredClassPatterns: ["EmitTrace"]);

        Assert.Contains("StackTraceBuilderTests", stack, StringComparison.Ordinal);
        Assert.DoesNotContain("System.", stack, StringComparison.Ordinal);
    }

    private static void EmitTrace(TraceSource source) =>
        source.TraceEvent(TraceEventType.Information, 1, "marker");

    private sealed class CacheCapturingListener(Action<TraceEventCache?> capture) : TraceListener
    {
        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
        {
            capture(eventCache);
            base.TraceEvent(eventCache, source, eventType, id, message);
        }

        public override void Write(string? message) { }
        public override void WriteLine(string? message) { }
    }
}
