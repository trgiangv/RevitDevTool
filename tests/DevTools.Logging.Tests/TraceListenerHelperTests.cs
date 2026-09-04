using System.Diagnostics;
using DevTools.Logging;

namespace DevTools.Logging.Tests;

public sealed class TraceListenerHelperTests
{
    [Fact]
    public void RegisterTraceListeners_adds_once_and_skips_null()
    {
        var listener = new TestTraceListener();
        try
        {
            TraceListenerHelper.RegisterTraceListeners(null, listener, listener);
            Assert.Contains(listener, Trace.Listeners.Cast<TraceListener>());
        }
        finally
        {
            TraceListenerHelper.UnregisterTraceListeners(listener);
        }
    }

    [Fact]
    public void UnregisterTraceListeners_removes_listener()
    {
        var listener = new TestTraceListener();
        TraceListenerHelper.RegisterTraceListeners(listener);
        TraceListenerHelper.UnregisterTraceListeners(listener);
        Assert.DoesNotContain(listener, Trace.Listeners.Cast<TraceListener>());
    }

    private sealed class TestTraceListener : TraceListener
    {
        public override void Write(string? message) { }
        public override void WriteLine(string? message) { }
    }
}
