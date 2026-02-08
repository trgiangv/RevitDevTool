using RevitDevTool.CodeExecute.Models;
namespace RevitDevTool.CodeExecute.Interfaces;

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
    IEnumerable<BaseNode> TreeRoot { get; }

    /// <summary>
    /// Event raised when tree changes (reload, add, remove)
    /// </summary>
    event EventHandler? TreeChanged;

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
    Task LoadSavedPathsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default);

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
    BaseNode? RemoveNode(BaseNode node);

    /// <summary>
    /// Clear all nodes
    /// </summary>
    void ClearAll();

    /// <summary>
    /// Execute a node
    /// </summary>
    /// <param name="node">Node to execute</param>
    void Execute(BaseNode node);
}