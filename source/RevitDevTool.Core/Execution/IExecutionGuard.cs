using DevTools.Execution.Abstractions;

namespace RevitDevTool.Core.Execution;

/// <summary>
/// Factory for creating combined dialog + failure suppression scopes.
/// Injected into the host context executor to wrap all host-thread operations.
/// </summary>
public interface IExecutionGuard
{
    /// <summary>
    /// Begins a combined dialog and failure suppression scope for the given mode.
    /// Reference-counted: nested calls share the underlying event subscriptions.
    /// </summary>
    IDisposable Begin(ExecutionGuardMode mode);

    /// <summary>
    /// True if the most recent scope encountered an unresolvable failure that caused a rollback.
    /// Reset at the start of each <see cref="Begin"/> call.
    /// </summary>
    bool HadRollback { get; }

    /// <summary>
    /// Human-readable description of rolled-back failures, or null if none.
    /// Only populated when <see cref="HadRollback"/> is true.
    /// </summary>
    string? RollbackSummary { get; }

    /// <summary>
    /// Full internal log summary from the most recent scope (for host-layer debug logging).
    /// </summary>
    string? LastLogSummary { get; }
}
