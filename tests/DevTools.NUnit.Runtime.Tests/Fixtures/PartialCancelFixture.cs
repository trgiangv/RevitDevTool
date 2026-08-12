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
    [Test, Order(1)]
    public void CompletesFirst()
    {
        Interlocked.Exchange(ref PartialCancelState.FirstCompleted, 1);
    }

    [Test, Order(2)]
    public void BlocksSecond()
    {
        Interlocked.Exchange(ref PartialCancelState.SecondEntered, 1);
        Thread.Sleep(Timeout.Infinite);
    }
}
