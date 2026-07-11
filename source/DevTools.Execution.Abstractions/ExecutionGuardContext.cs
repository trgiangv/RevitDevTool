namespace DevTools.Execution.Abstractions;

/// <summary>
/// Ambient context for propagating <see cref="ExecutionGuardMode"/> from callers
/// (MCP handlers, pytest handlers) to the host context executor without changing
/// the <see cref="IHostContextExecutor"/> interface.
/// </summary>
/// <remarks>
/// Uses <see cref="AsyncLocal{T}"/> so each async flow (MCP call, pytest session)
/// gets independent mode without affecting other concurrent operations.
/// </remarks>
public static class ExecutionGuardContext
{
    private static readonly AsyncLocal<ExecutionGuardMode> CurrentMode = new();
    private static readonly AsyncLocal<string?> CurrentRollbackSummary = new();

    /// <summary>
    /// Gets or sets the guard mode for the current async flow.
    /// Defaults to <see cref="ExecutionGuardMode.Passthrough"/>.
    /// </summary>
    public static ExecutionGuardMode Mode
    {
        get => CurrentMode.Value;
        set => CurrentMode.Value = value;
    }

    /// <summary>
    /// After execution completes, non-null if a transaction was rolled back due to
    /// unresolvable failures. This is the ONLY feedback that matters for AI callers.
    /// Null when everything succeeded (including when warnings were silently dismissed).
    /// </summary>
    public static string? RollbackSummary
    {
        get => CurrentRollbackSummary.Value;
        set => CurrentRollbackSummary.Value = value;
    }
}
