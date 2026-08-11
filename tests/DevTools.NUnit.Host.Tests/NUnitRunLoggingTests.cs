using DevTools.NUnit.Host.Logging;

namespace DevTools.NUnit.Host.Tests;

public sealed class NUnitRunLoggingTests
{
    [Fact]
    public void OutputMerger_combines_nunit_and_trace_output()
    {
        var merged = NUnitOutputMerger.Merge("line-a", "line-b");

        Assert.Equal($"line-a{Environment.NewLine}line-b", merged);
    }

    [Fact]
    public void OutputTracker_buffers_trace_for_active_test()
    {
        var tracker = new NUnitRunOutputTracker();
        tracker.BeginTest("1", "Sample");
        tracker.Append("trace-line");

        var output = tracker.Complete("1");

        Assert.Equal("trace-line", output);
    }
}
