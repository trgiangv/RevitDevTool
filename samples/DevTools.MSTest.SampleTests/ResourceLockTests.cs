namespace DevTools.MSTest.SampleTests;

// [DoNotParallelize] and [ResourceLock]. The host run also disables parallelization.

[DoNotParallelize]
[TestClass]
public sealed class ResourceLockTests
{
    private static int Held;

    [TestMethod]
    [ResourceLock("mstest-sample-gate")]
    public void Locked_section_observes_its_own_increment()
    {
        var seen = Interlocked.Increment(ref Held);
        Assert.IsGreaterThan(0, seen);
    }
}
