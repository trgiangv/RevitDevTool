using RevitDevTool.Execution.Interfaces;
using RevitDevTool.Execution.Models;
namespace RevitDevTool.Execution.Services;

/// <summary>
/// Handles all tree state operations in a centralized manner.
/// </summary>
public sealed class TreeStateManager : ITreeStateManager
{
    public TreeState CaptureState(IEnumerable<BaseNode> nodes)
    {
        var state = new TreeState();
        CaptureRecursive(nodes, state);
        return state;
    }

    public void RestoreState(IEnumerable<BaseNode> nodes, TreeState state, bool autoExpandNew = false)
    {
        RestoreRecursive(nodes, state, autoExpandNew);
    }

    #region Private Helpers

    private static void CaptureRecursive(IEnumerable<BaseNode> nodes, TreeState state)
    {
        foreach (var node in nodes)
        {
            // Capture expand state
            state.ExpandedStates[node.Id] = node.IsExpanded;

            // Capture selection
            if (node.IsSelected)
            {
                state.SelectedNodeId = node.Id;
            }

            // Capture last executed
            if (node.IsLastExecuted)
            {
                state.LastExecutedNodeId = node.Id;
            }

            // Recurse children
            if (node.Children.Count > 0)
            {
                CaptureRecursive(node.Children, state);
            }
        }
    }

    private static bool RestoreRecursive(IEnumerable<BaseNode> nodes, TreeState state, bool autoExpandNew)
    {
        var hasAnyNewDescendants = false;

        foreach (var node in nodes)
        {
            var isNewNode = !state.ExpandedStates.ContainsKey(node.Id);

            // Restore node properties
            RestoreNodeProperties(node, state, autoExpandNew, ref hasAnyNewDescendants);

            // Handle children
            if (node.Children.Count <= 0) continue;
            var hasNewDescendants = RestoreRecursive(node.Children, state, autoExpandNew);
            HandleAncestorExpansion(node, hasNewDescendants, isNewNode, autoExpandNew);
            hasAnyNewDescendants = hasAnyNewDescendants || hasNewDescendants;
        }

        return hasAnyNewDescendants;
    }

    private static void RestoreNodeProperties(BaseNode node, TreeState state, bool autoExpandNew, ref bool hasAnyNewDescendants)
    {
        // Restore expand state
        if (state.ExpandedStates.TryGetValue(node.Id, out var isExpanded))
        {
            node.IsExpanded = isExpanded;
        }
        else if (autoExpandNew)
        {
            node.IsExpanded = ShouldExpandNewNode(node);
            hasAnyNewDescendants = true;
        }

        // Restore selection
        if (node.Id == state.SelectedNodeId)
        {
            node.IsSelected = true;
        }

        // Restore last executed marker
        if (node.Id == state.LastExecutedNodeId)
        {
            node.IsLastExecuted = true;
        }
    }

    private static bool ShouldExpandNewNode(BaseNode node)
    {
        return node is { NodeType: NodeType.Container, Children.Count: > 0 };
    }

    private static void HandleAncestorExpansion(BaseNode node, bool hasNewDescendants, bool isNewNode, bool autoExpandNew)
    {
        // If this container has new descendants anywhere in its subtree, force expand it
        // This ensures all ancestors of new nodes are expanded so user can see them
        if (hasNewDescendants && !isNewNode && autoExpandNew)
        {
            node.IsExpanded = true;
        }
    }

    #endregion
}