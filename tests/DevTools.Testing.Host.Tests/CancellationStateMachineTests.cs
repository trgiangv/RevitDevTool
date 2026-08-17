using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host;

namespace DevTools.Testing.Host.Tests;

public sealed class CancellationStateMachineTests
{
    [Fact]
    public void Ordered_transition_requested_acknowledged_completed()
    {
        var machine = new TestingCancellationStateMachine();
        Assert.True(machine.TryTransition(TestingCancellationState.Requested));
        Assert.True(machine.TryTransition(TestingCancellationState.Acknowledged));
        Assert.True(machine.TryTransition(TestingCancellationState.Completed));
        Assert.False(machine.TryTransition(TestingCancellationState.Poisoned));
        Assert.Equal(TestingCancellationState.Completed, machine.State);
    }

    [Fact]
    public void Acknowledged_may_poison()
    {
        var machine = new TestingCancellationStateMachine();
        machine.Transition(TestingCancellationState.Requested);
        machine.Transition(TestingCancellationState.Acknowledged);
        machine.Transition(TestingCancellationState.Poisoned);
        Assert.Equal(TestingCancellationState.Poisoned, machine.State);
        Assert.False(machine.TryTransition(TestingCancellationState.Completed));
    }

    [Fact]
    public void Skips_are_rejected()
    {
        var machine = new TestingCancellationStateMachine();
        Assert.False(machine.TryTransition(TestingCancellationState.Acknowledged));
        Assert.False(machine.TryTransition(TestingCancellationState.Completed));
    }
}
