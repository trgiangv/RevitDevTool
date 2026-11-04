using DevTools.Execution.Models;
namespace DevTools.Execution.Interfaces;

/// <summary>
/// Owns Command Execution: the root tree, its saved paths, file watching, and execution.
/// The panel and the <c>run_command</c> MCP tool both drive it. Call it on the host UI thread.
/// </summary>
public interface IExecutionOrchestrator
{
    /// <summary>
    /// Root nodes of the current tree
    /// </summary>
    IEnumerable<ExecutionNodeBase> TreeRoot { get; }

    /// <summary>
    /// Event raised when tree changes (reload, add, remove)
    /// </summary>
    event EventHandler? TreeChanged;

    /// <summary>
    /// Event raised to publish execution progress text for UI feedback.
    /// </summary>
    event EventHandler<ExecutionProgressEventArgs>? ExecutionProgressChanged;

    /// <summary>
    /// Load the saved paths once. Paths that no longer load are dropped from the settings.
    /// Later calls return without loading again.
    /// </summary>
    Task LoadSavedPathsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Load an assembly or a script folder as a root and save its path.
    /// A path that is already a root, or that a loaded script root covers, is left as is.
    /// </summary>
    Task LoadFromPathAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// The root at <paramref name="path"/>, or the script root that contains it.
    /// </summary>
    ExecutionNodeRoot? FindRoot(string path);

    /// <summary>
    /// Reload every root.
    /// </summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reload one root from disk. Returns the root now in the tree, or null when it no longer loads.
    /// </summary>
    Task<ExecutionNodeRoot?> ReloadRootAsync(ExecutionNodeRoot root, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a node, and its parents that become empty. A removed root's path is dropped from the settings.
    /// Returns the next sibling for selection, or null.
    /// </summary>
    ExecutionNodeBase? RemoveNode(ExecutionNodeBase node);

    /// <summary>
    /// Remove every root and its saved path.
    /// </summary>
    void ClearAll();

    /// <summary>
    /// Execute a node and mark it as the last executed one.
    /// </summary>
    Task<ExecutionResult> ExecuteAsync(ExecutionNodeBase node, CancellationToken cancellationToken = default);
}

public sealed class ExecutionProgressEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}
