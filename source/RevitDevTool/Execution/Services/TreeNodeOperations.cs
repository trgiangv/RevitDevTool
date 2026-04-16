using System.Collections.ObjectModel;
using RevitDevTool.Execution.Models;
namespace RevitDevTool.Execution.Services;

internal static class TreeNodeOperations
{
    public static void MergeNodesIntoTree(ObservableCollection<ExecutionNodeBase> treeRoot, IEnumerable<ExecutionNodeBase> incomingNodes)
    {
        foreach (var incomingNode in incomingNodes)
        {
            var existingNode = treeRoot.FirstOrDefault(n => n.Id == incomingNode.Id);
            if (existingNode == null)
            {
                treeRoot.Add(incomingNode);
            }
            else
            {
                MergeChildrenRecursive(existingNode, incomingNode);
            }
        }
    }

    public static void ReplaceRootSnapshot(ObservableCollection<ExecutionNodeBase> treeRoot, IReadOnlyCollection<ExecutionNodeRoot> roots)
    {
        treeRoot.Clear();
        foreach (var root in roots)
        {
            treeRoot.Add(root);
        }
    }

    public static HashSet<string> CollectExecutableIdSet(IEnumerable<ExecutionNodeBase> nodes)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        CollectExecutableIdsRecursive(nodes, ids);
        return ids;
    }

    public static ExecutionNodeBase? PromoteLatestNewExecutable(IEnumerable<ExecutionNodeBase> nodes, HashSet<string> previousExecutableIds)
    {
        var nodeSnapshot = nodes as IReadOnlyList<ExecutionNodeBase> ?? nodes.ToList();
        var lastNewExecutable = FindLatestNewExecutableRecursive(nodeSnapshot, previousExecutableIds);
        if (lastNewExecutable == null) return null;

        ClearLastExecutedFlagRecursive(nodeSnapshot);
        lastNewExecutable.IsLastExecuted = true;
        return lastNewExecutable;
    }

    public static (bool Removed, ExecutionNodeBase? NextSelection) RemoveNodeWithCascade(
        ObservableCollection<ExecutionNodeBase> treeRoot,
        ExecutionNodeBase node,
        Action<string> onRootRemoved)
    {
        if (node is ExecutionNodeRoot rootNode)
            onRootRemoved(rootNode.RootPath);

        var rootIndex = treeRoot.IndexOf(node);
        if (rootIndex >= 0)
        {
            var nextSelection = GetNextSibling(treeRoot, rootIndex);
            treeRoot.RemoveAt(rootIndex);
            return (true, nextSelection);
        }

        foreach (var root in treeRoot.ToList())
        {
            var result = RemoveNodeRecursive(root, node, treeRoot, onRootRemoved);
            if (result.Removed) return result;
        }

        return (false, null);
    }

    private static void MergeChildrenRecursive(ExecutionNodeBase existing, ExecutionNodeBase incoming)
    {
        foreach (var incomingChild in incoming.Children)
        {
            var existingChild = existing.Children.FirstOrDefault(c => c.Id == incomingChild.Id);
            if (existingChild == null)
            {
                existing.Children.Add(incomingChild);
            }
            else
            {
                MergeChildrenRecursive(existingChild, incomingChild);
            }
        }

        var childrenToRemove = existing.Children.Where(c => incoming.Children.All(uc => uc.Id != c.Id)).ToList();
        foreach (var child in childrenToRemove)
        {
            existing.Children.Remove(child);
        }
    }

    private static void CollectExecutableIdsRecursive(IEnumerable<ExecutionNodeBase> nodes, HashSet<string> ids)
    {
        foreach (var node in nodes)
        {
            if (node.IsExecutable) ids.Add(node.Id);
            if (node.Children.Count > 0) CollectExecutableIdsRecursive(node.Children, ids);
        }
    }

    private static ExecutionNodeBase? FindLatestNewExecutableRecursive(IEnumerable<ExecutionNodeBase> nodes, HashSet<string> previousExecutableIds)
    {
        ExecutionNodeBase? last = null;

        foreach (var node in nodes)
        {
            if (node.IsExecutable && !previousExecutableIds.Contains(node.Id))
                last = node;

            if (node.Children.Count > 0)
            {
                var childLast = FindLatestNewExecutableRecursive(node.Children, previousExecutableIds);
                if (childLast != null) last = childLast;
            }
        }

        return last;
    }

    private static void ClearLastExecutedFlagRecursive(IEnumerable<ExecutionNodeBase> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsLastExecuted = false;
            if (node.Children.Count > 0) ClearLastExecutedFlagRecursive(node.Children);
        }
    }

    private static (bool Removed, ExecutionNodeBase? NextSelection) RemoveNodeRecursive(
        ExecutionNodeBase parent,
        ExecutionNodeBase toRemove,
        IList<ExecutionNodeBase> parentCollection,
        Action<string> onRootRemoved)
    {
        var childIndex = parent.Children.IndexOf(toRemove);
        return childIndex >= 0
            ? RemoveChildAtIndex(parent, childIndex, parentCollection, onRootRemoved)
            : RemoveFromChildrenRecursive(parent, toRemove, parentCollection, onRootRemoved);
    }

    private static (bool Removed, ExecutionNodeBase? NextSelection) RemoveChildAtIndex(
        ExecutionNodeBase parent,
        int childIndex,
        IList<ExecutionNodeBase> parentCollection,
        Action<string> onRootRemoved)
    {
        var nextSelection = GetNextSibling(parent.Children, childIndex);
        parent.Children.RemoveAt(childIndex);
        return TryCascadeDeleteEmptyParent(parent, parentCollection, nextSelection, onRootRemoved);
    }

    private static (bool Removed, ExecutionNodeBase? NextSelection) RemoveFromChildrenRecursive(
        ExecutionNodeBase parent,
        ExecutionNodeBase toRemove,
        IList<ExecutionNodeBase> parentCollection,
        Action<string> onRootRemoved)
    {
        foreach (var child in parent.Children.ToList())
        {
            var result = RemoveNodeRecursive(child, toRemove, parent.Children, onRootRemoved);
            if (!result.Removed) continue;

            if (parent.Children.Count == 0 && parent.NodeType == NodeType.Container)
                return TryCascadeDeleteEmptyParent(parent, parentCollection, result.NextSelection, onRootRemoved);

            return result;
        }

        return (false, null);
    }

    private static (bool Removed, ExecutionNodeBase? NextSelection) TryCascadeDeleteEmptyParent(
        ExecutionNodeBase parent,
        IList<ExecutionNodeBase> parentCollection,
        ExecutionNodeBase? currentNextSelection,
        Action<string> onRootRemoved)
    {
        if (parent.Children.Count > 0 || parent.NodeType != NodeType.Container)
            return (true, currentNextSelection);

        var parentIndex = parentCollection.IndexOf(parent);
        if (parentIndex < 0)
            return (true, currentNextSelection);

        if (parent is ExecutionNodeRoot rootNode)
            onRootRemoved(rootNode.RootPath);

        var nextSelection = GetNextSibling(parentCollection, parentIndex);
        parentCollection.RemoveAt(parentIndex);
        return (true, nextSelection);
    }

    private static ExecutionNodeBase? GetNextSibling(IList<ExecutionNodeBase> siblings, int removedIndex)
    {
        if (siblings.Count <= 1) return null;
        return removedIndex < siblings.Count - 1 ? siblings[removedIndex + 1] : siblings[removedIndex - 1];
    }
}
