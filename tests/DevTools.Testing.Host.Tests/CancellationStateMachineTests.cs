using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host;

namespace DevTools.Testing.Host.Tests;

public sealed class CancellationStateMachineTests
{
    [Fact]
    public void Ordered_transition_requested_acknowledged_completed()
    {
        var machine = new TestingCancellationStateMachine();
        Assert.True(machine.TryTransition(TestCancellationState.Requested));
        Assert.True(machine.TryTransition(TestCancellationState.Acknowledged));
        Assert.True(machine.TryTransition(TestCancellationState.Completed));
        Assert.False(machine.TryTransition(TestCancellationState.Poisoned));
        Assert.Equal(TestCancellationState.Completed, machine.State);
    }

    [Fact]
    public void Acknowledged_may_poison()
    {
        var machine = new TestingCancellationStateMachine();
        machine.Transition(TestCancellationState.Requested);
        machine.Transition(TestCancellationState.Acknowledged);
        machine.Transition(TestCancellationState.Poisoned);
        Assert.Equal(TestCancellationState.Poisoned, machine.State);
        Assert.False(machine.TryTransition(TestCancellationState.Completed));
    }

    [Fact]
    public void Skips_are_rejected()
    {
        var machine = new TestingCancellationStateMachine();
        Assert.False(machine.TryTransition(TestCancellationState.Acknowledged));
        Assert.False(machine.TryTransition(TestCancellationState.Completed));
    }

    [Fact]
    public void Reset_returns_to_none()
    {
        var machine = new TestingCancellationStateMachine();
        machine.Transition(TestCancellationState.Requested);
        machine.Transition(TestCancellationState.Poisoned);
        machine.Reset();
        Assert.Equal(TestCancellationState.None, machine.State);
        Assert.True(machine.TryTransition(TestCancellationState.Requested));
    }
}
