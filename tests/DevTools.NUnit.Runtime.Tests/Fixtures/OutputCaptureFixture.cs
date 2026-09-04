using NUnit.Framework;

namespace DevTools.NUnit.Runtime.Tests.Fixtures;

[TestFixture]
public sealed class OutputCaptureFixture
{
    [Test]
    public void Writes_console_trace_and_debug_markers()
    {
        Console.WriteLine("spike-output-marker");
        System.Diagnostics.Trace.WriteLine("spike-trace-marker");
        System.Diagnostics.Debug.WriteLine("spike-debug-marker");
        Assert.Pass();
    }
}
