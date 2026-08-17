using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Host;

public sealed class TestingCancellationStateMachine
{
    public TestingCancellationState State { get; private set; } = TestingCancellationState.None;

    public bool TryTransition(TestingCancellationState next)
    {
        if (!IsAllowed(State, next))
            return false;

        State = next;
        return true;
    }

    public void Transition(TestingCancellationState next)
    {
        if (TryTransition(next))
            return;

        throw new InvalidOperationException(
            $"Invalid cancellation transition {State} -> {next}.");
    }

    public static bool IsTerminal(TestingCancellationState state) =>
        state is TestingCancellationState.Completed or TestingCancellationState.Poisoned;

    private static bool IsAllowed(TestingCancellationState current, TestingCancellationState next) =>
        current switch
        {
            TestingCancellationState.None => next == TestingCancellationState.Requested,
            TestingCancellationState.Requested => next is TestingCancellationState.Acknowledged
                or TestingCancellationState.Poisoned,
            TestingCancellationState.Acknowledged => next is TestingCancellationState.Completed
                or TestingCancellationState.Poisoned,
            _ => false,
        };
}
