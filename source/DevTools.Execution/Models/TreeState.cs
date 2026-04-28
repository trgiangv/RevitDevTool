namespace DevTools.Execution.Models;

/// <summary>
/// State snapshot of the tree structure.
/// Used for capturing and restoring tree state (expand/collapse, selection, last executed, etc.)
/// </summary>
public sealed class TreeState
{
    /// <summary>
    /// Expanded states keyed by node Id
    /// </summary>
    public Dictionary<string, bool> ExpandedStates { get; } = [];

    /// <summary>
    /// Selected node Id
    /// </summary>
    public string? SelectedNodeId { get; set; }

    /// <summary>
    /// Last executed node Id
    /// </summary>
    public string? LastExecutedNodeId { get; set; }
}