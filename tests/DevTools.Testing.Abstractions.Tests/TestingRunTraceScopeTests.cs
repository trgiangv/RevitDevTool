using System.Diagnostics;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.Testing.Abstractions.Tests;

[CollectionDefinition(nameof(TestingRunTraceScopeTests), DisableParallelization = true)]
public sealed class TestingRunTraceScopeCollection;

[Collection(nameof(TestingRunTraceScopeTests))]
public sealed class TestingRunTraceScopeTests
{
    [Fact]
    public void CompleteCase_captures_trace()
    {
        using var scope = new TestingRunTraceScope();
        Trace.WriteLine("trace-marker");
        var captured = scope.CompleteCase();

        Assert.Contains("trace-marker", captured, StringComparison.Ordinal);
        Assert.Null(scope.CompleteCase());
    }

    [Fact]
    public void CompleteCase_captures_trace_and_debug_when_extra_listeners_exist()
    {
        var front = new RecordingTraceListener();
        Trace.Listeners.Insert(0, front);
        try
        {
            using var scope = new TestingRunTraceScope();
            Trace.WriteLine("trace-marker");
            Debug.WriteLine("debug-marker");
            var captured = scope.CompleteCase();

            Assert.Contains("trace-marker", captured, StringComparison.Ordinal);
            Assert.Contains("debug-marker", captured, StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(front);
        }
    }

    [Fact]
    public void Dispose_restores_trace_listeners_from_before_scope()
    {
        var front = new RecordingTraceListener();
        var beforeCount = Trace.Listeners.Count;
        Trace.Listeners.Insert(0, front);
        try
        {
            using (var scope = new TestingRunTraceScope())
            {
                Assert.True(Trace.Listeners.Contains(front));
            }

            Assert.True(Trace.Listeners.Contains(front));
            Assert.Equal(beforeCount + 1, Trace.Listeners.Count);
        }
        finally
        {
            Trace.Listeners.Remove(front);
        }
    }

    [Fact]
    public void WriteThrough_reaches_trace_without_refilling_the_ide_buffer()
    {
        using var scope = new TestingRunTraceScope();
        var pane = new RecordingTraceListener();
        Trace.Listeners.Add(pane);
        try
        {
            scope.WriteThrough("console-marker\r\n");
            Assert.Null(scope.CompleteCase());
            Assert.Contains("console-marker", pane.Text, StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(pane);
        }
    }

    [Theory]
    [InlineData("console", "trace", "console\ntrace")]
    [InlineData("console", null, "console")]
    [InlineData(null, "trace", "trace")]
    public void Merge_joins_framework_console_then_trace(string? framework, string? trace, string expected)
    {
        var merged = TestingRunTraceScope.Merge(framework, trace);
        Assert.Equal(expected.Replace("\n", Environment.NewLine), merged);
    }

    [Fact]
    public void Merge_returns_null_when_both_blank()
    {
        Assert.Null(TestingRunTraceScope.Merge(null, "  "));
        Assert.Null(TestingRunTraceScope.Merge(" ", null));
    }

    private sealed class RecordingTraceListener : TraceListener
    {
        private readonly System.Text.StringBuilder _buffer = new();

        public string Text
        {
            get
            {
                lock (_buffer)
                    return _buffer.ToString();
            }
        }

        public override void Write(string? message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            lock (_buffer)
                _buffer.Append(message);
        }

        public override void WriteLine(string? message) =>
            Write(string.IsNullOrEmpty(message) ? Environment.NewLine : message + Environment.NewLine);
    }
}
