using NUnit.Framework;
using System.Threading;

namespace DevTools.NUnit.Runtime.Tests.Fixtures;

public static class PartialCancelState
{
    public static int FirstCompleted;
    public static int SecondEntered;
}

[TestFixture]
public sealed class PartialCancelFixture
{
    [Test]
    public void CompletesFirst()
    {
        Interlocked.Exchange(ref PartialCancelState.FirstCompleted, 1);
    }

    [Test, DependsOnTest(nameof(CompletesFirst))]
    public void BlocksSecond()
    {
        Interlocked.Exchange(ref PartialCancelState.SecondEntered, 1);
        Thread.Sleep(Timeout.Infinite);
    }
}
