namespace DevTools.TUnit.SampleTests;

// Scope: [Timeout] with CancellationToken injection.

public sealed class TimeoutTests
{
    [Test]
    [Timeout(5000)]
    public async Task Completes_within_timeout(CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken);
        var completed = true;
        await Assert.That(completed).IsTrue();
    }
}
