using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevTools.MSTest.Runtime.Tests.Fixtures;

public static class Probe
{
    public static int Started;
    public static int Finished;
}

[TestClass]
public class CancelTests
{
    public TestContext? TestContext { get; set; }

    [TestMethod]
    public void Blocks()
    {
        Interlocked.Exchange(ref Probe.Started, 1);
        var cancellationToken = TestContext!.CancellationToken;
        cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(15));
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Exchange(ref Probe.Finished, 1);
    }
}
