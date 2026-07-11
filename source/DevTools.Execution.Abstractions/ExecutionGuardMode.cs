namespace DevTools.Execution.Abstractions;

/// <summary>
/// Controls how the execution guard handles Revit dialogs and transaction failures.
/// </summary>
public enum ExecutionGuardMode
{
    /// <summary>
    /// No suppression — dialogs and failure UI behave normally.
    /// Default for interactive UI execution (command tree, debugger).
    /// </summary>
    Passthrough,

    /// <summary>
    /// Auto-dismiss dialogs and auto-resolve/rollback failures.
    /// Used by MCP tool calls and pytest sessions where no human can interact.
    /// </summary>
    Suppress,
}
