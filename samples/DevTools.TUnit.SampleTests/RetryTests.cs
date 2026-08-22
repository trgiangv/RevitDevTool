namespace DevTools.TUnit.SampleTests;

// Scope: [Retry(n)] re-executes failed tests up to n additional attempts.

public sealed class RetryTests
{
    static int Attempts;

    [Test]
    [Retry(2)]
    public async Task Succeeds_after_retry()
    {
        var attempt = Interlocked.Increment(ref Attempts);
        if (attempt == 1)
            throw new InvalidOperationException("First attempt is expected to fail.");
        await Assert.That(attempt).IsGreaterThan(1);
    }
}
