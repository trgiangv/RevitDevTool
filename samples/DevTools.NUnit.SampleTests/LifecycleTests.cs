using NUnit.Framework;

namespace DevTools.NUnit.SampleTests;

// Discover: [Repeat] (one leaf), async, [Explicit], [Ignore].

[TestFixture]
public sealed class LifecycleTests
{
    [Test]
    [Repeat(3)]
    public void Repeat_is_one_discovered_node()
    {
        Assert.Pass();
    }

    [Test]
    public async Task Async_delay_then_pass()
    {
        await Task.Delay(1);
        Assert.Pass();
    }

    [Explicit("Listed; run only with --filter Explicit_is_listed_not_run")]
    [Test]
    public void Explicit_is_listed_not_run()
    {
        Assert.Fail("Must not run unless selected.");
    }

    [Ignore("Listed; must not execute.")]
    [Test]
    public void Ignored_is_listed()
    {
        Assert.Fail("Ignored tests must not execute.");
    }
}
