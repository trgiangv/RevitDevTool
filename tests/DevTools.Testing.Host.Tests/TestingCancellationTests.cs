using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Host;

namespace DevTools.Testing.Host.Tests;

[TestClass]
public sealed class TestingCancellationTests
{
    [TestMethod]
    public void Ordered_transition_requested_acknowledged_completed()
    {
        var cancellation = new TestingCancellation();
        Assert.IsTrue(cancellation.TryTransition(TestCancellationState.Requested));
        Assert.IsTrue(cancellation.TryTransition(TestCancellationState.Acknowledged));
        Assert.IsTrue(cancellation.TryTransition(TestCancellationState.Completed));
        Assert.IsFalse(cancellation.TryTransition(TestCancellationState.Poisoned));
        Assert.AreEqual(TestCancellationState.Completed, cancellation.State);
    }

    [TestMethod]
    public void Acknowledged_may_poison()
    {
        var cancellation = new TestingCancellation();
        cancellation.Transition(TestCancellationState.Requested);
        cancellation.Transition(TestCancellationState.Acknowledged);
        cancellation.Transition(TestCancellationState.Poisoned);
        Assert.AreEqual(TestCancellationState.Poisoned, cancellation.State);
        Assert.IsFalse(cancellation.TryTransition(TestCancellationState.Completed));
    }

    [TestMethod]
    public void Skips_are_rejected()
    {
        var cancellation = new TestingCancellation();
        Assert.IsFalse(cancellation.TryTransition(TestCancellationState.Acknowledged));
        Assert.IsFalse(cancellation.TryTransition(TestCancellationState.Completed));
    }

    [TestMethod]
    public void Reset_returns_to_none()
    {
        var cancellation = new TestingCancellation();
        cancellation.Transition(TestCancellationState.Requested);
        cancellation.Transition(TestCancellationState.Poisoned);
        cancellation.Reset();
        Assert.AreEqual(TestCancellationState.None, cancellation.State);
        Assert.IsTrue(cancellation.TryTransition(TestCancellationState.Requested));
    }
}
