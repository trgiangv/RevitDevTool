using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevTools.MSTest.Runtime.Tests.Fixtures;

public static class Probe
{
    public static int Executions;
    public static string? Last;
    public static int Step;
}

[TestClass]
public class SelectionTests
{
    [TestMethod]
    public void Alpha() => Hit("Alpha");

    [TestMethod]
    public void Beta() => Hit("Beta");

    [TestMethod]
    public void Gamma() => Hit("Gamma");

    private static void Hit(string name)
    {
        Probe.Last = name;
        Interlocked.Increment(ref Probe.Executions);
    }
}

[TestClass]
public class ChainTests
{
    [TestMethod]
    public void One() => Probe.Step = 1;

    [TestMethod]
    [DependsOn(nameof(One))]
    public void Two()
    {
        Assert.AreEqual(1, Probe.Step);
        Probe.Step = 2;
    }

    [TestMethod]
    [DependsOn(nameof(Two))]
    public void Three()
    {
        Console.WriteLine("chain-step=" + Probe.Step);
        Assert.AreEqual(2, Probe.Step);
    }
}
