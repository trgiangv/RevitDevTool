using NUnit.Framework;
using System.Threading;

namespace DevTools.NUnit.Runtime.Tests.Fixtures;

public static class BlockingRunState
{
    public static int Entered;
    public static int Release;
}

[TestFixture]
public sealed class BlockingFixture
{
    [Test]
    public void Blocks_UntilRunStopped()
    {
        Interlocked.Exchange(ref BlockingRunState.Entered, 1);
        while (Volatile.Read(ref BlockingRunState.Release) == 0)
            Thread.Sleep(10);
    }
}
