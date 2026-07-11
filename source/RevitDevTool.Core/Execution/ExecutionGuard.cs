using DevTools.Execution.Abstractions;

namespace RevitDevTool.Core.Execution;

/// <summary>
/// Factory that creates combined dialog + failure suppression scopes based on mode.
/// Pure Revit-layer component — no logging dependencies.
/// Registered as singleton in DI; injected into <c>RevitHostContextExecutor</c>.
/// </summary>
public sealed class ExecutionGuard : IExecutionGuard
{
    private ExecutionGuardFeedback? _lastFeedback;

    public bool HadRollback => _lastFeedback?.HadRollback ?? false;
    public string? RollbackSummary => _lastFeedback?.GetRollbackSummary();
    public string? LastLogSummary => _lastFeedback?.ToLogSummary();

    public IDisposable Begin(ExecutionGuardMode mode)
    {
        if (mode == ExecutionGuardMode.Passthrough)
            return NoOpDisposable.Instance;

        var feedback = new ExecutionGuardFeedback();
        _lastFeedback = feedback;
        return new ActiveScope(feedback);
    }

    private sealed class ActiveScope(ExecutionGuardFeedback feedback) : IDisposable
    {
        private readonly DialogSuppressionScope _dialogScope = new(feedback);
        private readonly FailureSuppressionScope _failureScope = new(feedback);
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _failureScope.Dispose();
            _dialogScope.Dispose();
        }
    }

    private sealed class NoOpDisposable : IDisposable
    {
        internal static readonly NoOpDisposable Instance = new();
        public void Dispose() { }
    }
}
