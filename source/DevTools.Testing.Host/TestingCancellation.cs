using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Host;

public sealed class TestingCancellation
{
    public TestCancellationState State { get; private set; } = TestCancellationState.None;

    public bool IsTerminal =>
        State is TestCancellationState.Completed or TestCancellationState.Poisoned;

    public bool TryTransition(TestCancellationState next)
    {
        if (!IsAllowed(State, next))
            return false;

        State = next;
        return true;
    }

    public void Transition(TestCancellationState next)
    {
        if (TryTransition(next))
            return;

        throw new InvalidOperationException(
            $"Invalid cancellation transition {State} -> {next}.");
    }

    public void Reset() => State = TestCancellationState.None;

    private static bool IsAllowed(TestCancellationState current, TestCancellationState next) =>
        current switch
        {
            TestCancellationState.None => next == TestCancellationState.Requested,
            TestCancellationState.Requested => next is TestCancellationState.Acknowledged
                or TestCancellationState.Poisoned,
            TestCancellationState.Acknowledged => next is TestCancellationState.Completed
                or TestCancellationState.Poisoned,
            _ => false,
        };
}
