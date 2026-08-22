namespace DevTools.TUnit.SampleTests;

// Scope: async execution, Explicit listing, and Skip behavior.

public sealed class LifecycleTests
{
    [Test]
    public async Task Async_delay_then_pass()
    {
        await Task.Delay(50);
        var completed = true;
        await Assert.That(completed).IsTrue();
    }

    [Test]
    [Explicit("Listed; run only with --filter Explicit_is_listed_not_run")]
    public void Explicit_is_listed_not_run()
    {
        Assert.Fail("Must not run unless selected.");
    }

    [Test]
    [Skip("Listed; must not execute.")]
    public void Skipped_is_listed()
    {
        Assert.Fail("Skipped tests must not execute.");
    }
}
