namespace DevTools.TUnit.SampleTests;

// Scope: [Repeat(n)] expands n + 1 executions in TUnit Engine.

public sealed class RepeatTests
{
    static int _executions;

    [Test]
    [Repeat(3)]
    public async Task Repeat_expands_four_executions()
    {
        var execution = Interlocked.Increment(ref _executions);
        await Assert.That(execution).IsGreaterThan(0);
        await Assert.That(execution).IsLessThanOrEqualTo(4);
    }
}
