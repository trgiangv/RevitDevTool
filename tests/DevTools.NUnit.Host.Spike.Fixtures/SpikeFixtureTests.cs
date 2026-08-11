using NUnit.Framework;

namespace DevTools.NUnit.Host.Spike.Fixtures;

[TestFixture]
public sealed class SpikeFixtureTests
{
    [Test]
    public void Spike_Pass()
    {
        Assert.Pass();
    }

    [Test]
    public void Spike_Fail()
    {
        Assert.Fail("spike intentional failure");
    }

    [Test]
    public void Spike_Output()
    {
        Console.WriteLine("spike-output-marker");
        System.Diagnostics.Trace.WriteLine("spike-trace-marker");
        System.Diagnostics.Debug.WriteLine("spike-debug-marker");
        Assert.Pass();
    }
}
