namespace DevTools.MSTest.SampleTests;

// [Retry] runs the method again after a failure. One retry means two attempts.

[TestClass]
public sealed class RetryTests
{
    private static int Attempts;

    [TestMethod]
    [Retry(1)]
    public void Succeeds_after_retry()
    {
        var attempt = Interlocked.Increment(ref Attempts);
        if (attempt == 1)
            throw new InvalidOperationException("First attempt is expected to fail.");

        Assert.IsGreaterThan(1, attempt);
    }
}
