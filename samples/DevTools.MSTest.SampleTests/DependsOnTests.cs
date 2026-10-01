namespace DevTools.MSTest.SampleTests;

// [DependsOn] orders tests. A dependent test is skipped when its prerequisite fails.

[TestClass]
public sealed class DependsOnTests
{
    public static int Gate;
    public static int ChainStep;

    [TestMethod]
    public void Producer_sets_gate()
    {
        Gate = 7;
    }

    [TestMethod]
    [DependsOn(nameof(Producer_sets_gate))]
    public void Consumer_sees_gate()
    {
        Assert.AreEqual(7, Gate);
    }

    [TestMethod]
    public void Chain_step_one()
    {
        ChainStep = 1;
    }

    [TestMethod]
    [DependsOn(nameof(Chain_step_one))]
    public void Chain_step_two()
    {
        ChainStep = 2;
    }

    [TestMethod]
    [DependsOn(nameof(Chain_step_two))]
    public void Chain_step_three_sees_prior_state()
    {
        Console.WriteLine($"ChainStep = {ChainStep}");
        Assert.AreEqual(2, ChainStep);
    }
}
