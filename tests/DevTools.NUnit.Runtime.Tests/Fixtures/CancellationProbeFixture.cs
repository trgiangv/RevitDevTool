using NUnit.Framework;
using System.Threading;

namespace DevTools.NUnit.Runtime.Tests.Fixtures;

public static class CancellationProbeState
{
    public static int BodyEntered;
}

[TestFixture]
public sealed class CancellationProbeFixture
{
    [Test]
    public void BodyMustNotRunWhenRunIsCancelled()
    {
        Interlocked.Exchange(ref CancellationProbeState.BodyEntered, 1);
    }
}
