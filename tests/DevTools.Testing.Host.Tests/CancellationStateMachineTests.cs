using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host;

namespace DevTools.Testing.Host.Tests;

[TestClass]
public sealed class CancellationStateMachineTests
{
    [TestMethod]
    public void Ordered_transition_requested_acknowledged_completed()
    {
        var machine = new TestingCancellationStateMachine();
        Assert.IsTrue(machine.TryTransition(TestCancellationState.Requested));
        Assert.IsTrue(machine.TryTransition(TestCancellationState.Acknowledged));
        Assert.IsTrue(machine.TryTransition(TestCancellationState.Completed));
        Assert.IsFalse(machine.TryTransition(TestCancellationState.Poisoned));
        Assert.AreEqual(TestCancellationState.Completed, machine.State);
    }

    [TestMethod]
    public void Acknowledged_may_poison()
    {
        var machine = new TestingCancellationStateMachine();
        machine.Transition(TestCancellationState.Requested);
        machine.Transition(TestCancellationState.Acknowledged);
        machine.Transition(TestCancellationState.Poisoned);
        Assert.AreEqual(TestCancellationState.Poisoned, machine.State);
        Assert.IsFalse(machine.TryTransition(TestCancellationState.Completed));
    }

    [TestMethod]
    public void Skips_are_rejected()
    {
        var machine = new TestingCancellationStateMachine();
        Assert.IsFalse(machine.TryTransition(TestCancellationState.Acknowledged));
        Assert.IsFalse(machine.TryTransition(TestCancellationState.Completed));
    }

    [TestMethod]
    public void Reset_returns_to_none()
    {
        var machine = new TestingCancellationStateMachine();
        machine.Transition(TestCancellationState.Requested);
        machine.Transition(TestCancellationState.Poisoned);
        machine.Reset();
        Assert.AreEqual(TestCancellationState.None, machine.State);
        Assert.IsTrue(machine.TryTransition(TestCancellationState.Requested));
    }
}
