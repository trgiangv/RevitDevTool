using DevTools.Execution.Models;
namespace DevTools.Execution.Interfaces;

/// <summary>
/// Main orchestrator that coordinates all services and providers.
/// This is the main entry point for the ViewModel.
/// Follows Single Responsibility Principle: coordination only.
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
    /// Event raised when a root node is automatically removed (e.g. folder renamed/deleted externally).
    /// Consumers should use this to clean up persisted settings.
    /// </summary>
    event EventHandler<RootRemovedEventArgs>? RootRemoved;

    /// <summary>
    /// Event raised to publish execution progress text for UI feedback.
    /// </summary>
    event EventHandler<ExecutionProgressEventArgs>? ExecutionProgressChanged;

    /// <summary>
    /// Load nodes from a path using the current provider
    /// </summary>
    /// <param name="path">Path to load from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task LoadFromPathAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load all saved paths from settings (called on startup).
    /// Provider detection is automatic via CanHandle.
    /// </summary>
    /// <param name="paths">List of paths to load (assemblies, folders, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IReadOnlyList<string>> LoadSavedPathsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reload the current tree (e.g., after file changes)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a node from the tree.
    /// Returns the next sibling for selection, or null if none available.
    /// Also removes empty parent containers automatically.
    /// </summary>
    /// <param name="node">Node to remove</param>
    /// <returns>Next sibling to select, or null</returns>
    ExecutionNodeBase? RemoveNode(ExecutionNodeBase node);

    /// <summary>
    /// Clear all nodes
    /// </summary>
    void ClearAll();

    /// <summary>
    /// Execute a node
    /// </summary>
    /// <param name="node">Node to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<ExecutionResult> ExecuteAsync(ExecutionNodeBase node, CancellationToken cancellationToken = default);
}

public sealed class RootRemovedEventArgs(string rootPath, string? newPath = null) : EventArgs
{
    public string RootPath { get; } = rootPath;

    /// <summary>
    /// New path after rename (null if deleted)
    /// </summary>
    public string? NewPath { get; } = newPath;

    public bool IsRename => NewPath != null;
}

public sealed class ExecutionProgressEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}