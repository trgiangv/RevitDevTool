using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevTools.TestAdapter.Tests.Fixtures;

public static class Probe
{
    public static int Executions;
}

[TestClass]
public sealed class CounterTests
{
    [TestMethod]
    public void Increments()
    {
        Probe.Executions++;
        var directory = Path.GetDirectoryName(typeof(CounterTests).Assembly.Location);
        if (!string.IsNullOrEmpty(directory))
            File.WriteAllText(Path.Combine(directory, "mstest-body-executed.txt"), "1");
    }
}

[TestClass]
public sealed class ChainTests
{
    [TestMethod]
    public void One() { }

    [TestMethod]
    [DependsOn(nameof(One))]
    public void Two() { }

    [TestMethod]
    [DependsOn(nameof(Two))]
    public void Three() { }

    [TestMethod]
    public void Unrelated() { }
}

[TestClass]
[DependsOn(typeof(ChainTests))]
public sealed class AfterChainTests
{
    [TestMethod]
    public void Audit() { }
}
