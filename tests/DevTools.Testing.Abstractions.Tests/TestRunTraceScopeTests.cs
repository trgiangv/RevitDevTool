using System.Diagnostics;
using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.Testing.Abstractions.Tests;

[DoNotParallelize]
[TestClass]
public sealed class TestRunTraceScopeTests
{
    [TestMethod]
    public void CompleteCase_captures_trace()
    {
        using var scope = new TestRunTraceScope();
        Trace.WriteLine("trace-marker");
        var captured = scope.CompleteCase();

        Assert.Contains("trace-marker", captured!, StringComparison.Ordinal);
        Assert.IsNull(scope.CompleteCase());
    }

    [TestMethod]
    public void CompleteCase_captures_trace_and_debug_when_extra_listeners_exist()
    {
        var front = new RecordingTraceListener();
        Trace.Listeners.Insert(0, front);
        try
        {
            using var scope = new TestRunTraceScope();
            Trace.WriteLine("trace-marker");
            Debug.WriteLine("debug-marker");
            var captured = scope.CompleteCase();

            Assert.Contains("trace-marker", captured!, StringComparison.Ordinal);
            Assert.Contains("debug-marker", captured!, StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(front);
        }
    }

    [TestMethod]
    public void Dispose_restores_trace_listeners_from_before_scope()
    {
        var front = new RecordingTraceListener();
        var beforeCount = Trace.Listeners.Count;
        Trace.Listeners.Insert(0, front);
        try
        {
            using (var scope = new TestRunTraceScope())
            {
                Assert.IsTrue(Trace.Listeners.Contains(front));
            }

            Assert.IsTrue(Trace.Listeners.Contains(front));
            Assert.AreEqual(beforeCount + 1, Trace.Listeners.Count);
        }
        finally
        {
            Trace.Listeners.Remove(front);
        }
    }

    [TestMethod]
    public void WriteThrough_reaches_trace_without_refilling_the_ide_buffer()
    {
        using var scope = new TestRunTraceScope();
        var pane = new RecordingTraceListener();
        Trace.Listeners.Add(pane);
        try
        {
            scope.WriteThrough("console-marker\r\n");
            Assert.IsNull(scope.CompleteCase());
            Assert.Contains("console-marker", pane.Text, StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(pane);
        }
    }

    [TestMethod]
    [DataRow("console", "trace", "console\ntrace")]
    [DataRow("console", null, "console")]
    [DataRow(null, "trace", "trace")]
    public void Merge_joins_framework_console_then_trace(string? framework, string? trace, string expected)
    {
        var merged = TestRunTraceScope.Merge(framework, trace);
        Assert.AreEqual(expected.Replace("\n", Environment.NewLine), merged);
    }

    [TestMethod]
    public void Merge_returns_null_when_both_blank()
    {
        Assert.IsNull(TestRunTraceScope.Merge(null, "  "));
        Assert.IsNull(TestRunTraceScope.Merge(" ", null));
    }

    [TestMethod]
    public void CompleteCase_keeps_each_key_in_its_own_bucket()
    {
        var key = "row-a";
        using var scope = new TestRunTraceScope(() => key);
        Trace.WriteLine("trace-row-a");
        key = "row-b";
        Trace.WriteLine("trace-row-b");

        var first = scope.CompleteCase("row-a");
        var second = scope.CompleteCase("row-b");

        Assert.Contains("trace-row-a", first!, StringComparison.Ordinal);
        Assert.IsFalse(first!.Contains("trace-row-b", StringComparison.Ordinal));
        Assert.Contains("trace-row-b", second!, StringComparison.Ordinal);
        Assert.IsFalse(second!.Contains("trace-row-a", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Output_no_case_claims_reaches_the_pane_but_not_a_case()
    {
        var pane = new RecordingTraceListener();
        Trace.Listeners.Add(pane);
        try
        {
            var key = "fixture-setup";
            using var scope = new TestRunTraceScope(() => key);
            Trace.WriteLine("setup-marker");
            key = "case-a";
            Trace.WriteLine("case-marker");

            var captured = scope.CompleteCase("case-a")!;

            Assert.Contains("case-marker", captured, StringComparison.Ordinal);
            Assert.IsFalse(captured.Contains("setup-marker", StringComparison.Ordinal));
            Assert.Contains("setup-marker", pane.Text, StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(pane);
        }
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
