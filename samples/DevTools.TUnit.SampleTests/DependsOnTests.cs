namespace DevTools.TUnit.SampleTests;

// Scope: [DependsOn] ordering across tests in the same fixture.

public sealed class DependsOnTests
{
    public static int Gate;
    public static int ChainStep;

    [Test]
    public void Producer_sets_gate()
    {
        Gate = 7;
    }

    [Test]
    [DependsOn(nameof(Producer_sets_gate))]
    public async Task Consumer_sees_gate()
    {
        await Assert.That(Gate).IsEqualTo(7);
    }

    [Test]
    public void Chain_step_one()
    {
        ChainStep = 1;
    }

    [Test]
    [DependsOn(nameof(Chain_step_one))]
    public void Chain_step_two()
    {
        ChainStep = 2;
    }

    [Test]
    [DependsOn(nameof(Chain_step_two))]
    public async Task Chain_step_three_sees_prior_state()
    {
        await Assert.That(ChainStep).IsEqualTo(2);
    }
}
