using NUnit.Framework;

namespace DevTools.NUnit.Runtime.Tests.Fixtures;

[TestFixture]
public sealed class TraceRowFixture
{
    [TestCase(1)]
    [TestCase(2)]
    public void Trace_row(int row) =>
        System.Diagnostics.Trace.WriteLine("trace-row-" + row);
}
