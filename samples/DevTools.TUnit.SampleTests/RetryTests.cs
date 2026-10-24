namespace DevTools.TUnit.SampleTests;

// Scope: [Retry(n)] re-executes failed tests up to n additional attempts.

public sealed class RetryTests
{
    static int _attempts;

    [Test]
    [Retry(2)]
    public async Task Succeeds_after_retry()
    {
        var attempt = Interlocked.Increment(ref _attempts);
        if (attempt == 1)
            throw new InvalidOperationException("First attempt is expected to fail.");
        await Assert.That(attempt).IsGreaterThan(1);
    }
}
