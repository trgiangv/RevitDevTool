using DevTools.Execution.Models;
namespace DevTools.Execution.Interfaces;

/// <summary>
/// Service for managing tree state (expand/collapse, selection, highlights, etc.)
/// Centralized state management following Single Responsibility Principle.
/// </summary>
public interface ITreeStateManager
{
    /// <summary>
    /// Capture the current state of the tree
    /// </summary>
    /// <param name="nodes">Root nodes of the tree</param>
    /// <returns>Captured state</returns>
    TreeState CaptureState(IEnumerable<ExecutionNodeBase> nodes);

    /// <summary>
    /// Restore state to the tree
    /// </summary>
    /// <param name="nodes">Root nodes of the tree</param>
    /// <param name="state">State to restore</param>
    /// <param name="autoExpandNew">Whether to auto-expand new nodes not in the state</param>
    void RestoreState(IEnumerable<ExecutionNodeBase> nodes, TreeState state, bool autoExpandNew = false);
}