using NUnit.Framework;
using System.Threading;

namespace DevTools.NUnit.Runtime.Tests.Fixtures;

public static class BlockingRunState
{
    public static int Entered;
}

[TestFixture]
public sealed class BlockingFixture
{
    [Test]
    public void Blocks_UntilRunStopped()
    {
        Interlocked.Exchange(ref BlockingRunState.Entered, 1);
        Thread.Sleep(Timeout.Infinite);
    }
}
