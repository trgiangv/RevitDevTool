using System.Collections.ObjectModel;
using RevitDevTool.CodeExecute.Models;

namespace RevitDevTool.CodeExecute.Services;

internal static class TreeNodeOperations
{
    public static void MergeNodesIntoTree(ObservableCollection<BaseNode> treeRoot, IEnumerable<BaseNode> incomingNodes)
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

    public static void ReplaceRootSnapshot(ObservableCollection<BaseNode> treeRoot, IReadOnlyCollection<RootNode> roots)
    {
        treeRoot.Clear();
        foreach (var root in roots)
        {
            treeRoot.Add(root);
        }
    }

    public static HashSet<string> CollectExecutableIdSet(IEnumerable<BaseNode> nodes)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        CollectExecutableIdsRecursive(nodes, ids);
        return ids;
    }

    public static BaseNode? PromoteLatestNewExecutable(IEnumerable<BaseNode> nodes, HashSet<string> previousExecutableIds)
    {
        var nodeSnapshot = nodes as IReadOnlyList<BaseNode> ?? nodes.ToList();
        var lastNewExecutable = FindLatestNewExecutableRecursive(nodeSnapshot, previousExecutableIds);
        if (lastNewExecutable == null) return null;

        ClearLastExecutedFlagRecursive(nodeSnapshot);
        lastNewExecutable.IsLastExecuted = true;
        return lastNewExecutable;
    }

    public static (bool Removed, BaseNode? NextSelection) RemoveNodeWithCascade(
        ObservableCollection<BaseNode> treeRoot,
        BaseNode node,
        Action<string> onRootRemoved)
    {
        if (node is RootNode rootNode)
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

    private static void MergeChildrenRecursive(BaseNode existing, BaseNode incoming)
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

    private static void CollectExecutableIdsRecursive(IEnumerable<BaseNode> nodes, HashSet<string> ids)
    {
        foreach (var node in nodes)
        {
            if (node.IsExecutable) ids.Add(node.Id);
            if (node.Children.Count > 0) CollectExecutableIdsRecursive(node.Children, ids);
        }
    }

    private static BaseNode? FindLatestNewExecutableRecursive(IEnumerable<BaseNode> nodes, HashSet<string> previousExecutableIds)
    {
        BaseNode? last = null;

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

    private static void ClearLastExecutedFlagRecursive(IEnumerable<BaseNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsLastExecuted = false;
            if (node.Children.Count > 0) ClearLastExecutedFlagRecursive(node.Children);
        }
    }

    private static (bool Removed, BaseNode? NextSelection) RemoveNodeRecursive(
        BaseNode parent,
        BaseNode nodeToRemove,
        IList<BaseNode> parentCollection,
        Action<string> onRootRemoved)
    {
        var childIndex = parent.Children.IndexOf(nodeToRemove);
        return childIndex >= 0
            ? RemoveChildAtIndex(parent, childIndex, parentCollection, onRootRemoved)
            : RemoveFromChildrenRecursive(parent, nodeToRemove, parentCollection, onRootRemoved);
    }

    private static (bool Removed, BaseNode? NextSelection) RemoveChildAtIndex(
        BaseNode parent,
        int childIndex,
        IList<BaseNode> parentCollection,
        Action<string> onRootRemoved)
    {
        var nextSelection = GetNextSibling(parent.Children, childIndex);
        parent.Children.RemoveAt(childIndex);
        return TryCascadeDeleteEmptyParent(parent, parentCollection, nextSelection, onRootRemoved);
    }

    private static (bool Removed, BaseNode? NextSelection) RemoveFromChildrenRecursive(
        BaseNode parent,
        BaseNode nodeToRemove,
        IList<BaseNode> parentCollection,
        Action<string> onRootRemoved)
    {
        foreach (var child in parent.Children.ToList())
        {
            var result = RemoveNodeRecursive(child, nodeToRemove, parent.Children, onRootRemoved);
            if (!result.Removed) continue;

            if (parent.Children.Count == 0 && parent.NodeType == NodeType.Container)
                return TryCascadeDeleteEmptyParent(parent, parentCollection, result.NextSelection, onRootRemoved);

            return result;
        }

        return (false, null);
    }

    private static (bool Removed, BaseNode? NextSelection) TryCascadeDeleteEmptyParent(
        BaseNode parent,
        IList<BaseNode> parentCollection,
        BaseNode? currentNextSelection,
        Action<string> onRootRemoved)
    {
        if (parent.Children.Count > 0 || parent.NodeType != NodeType.Container)
            return (true, currentNextSelection);

        var parentIndex = parentCollection.IndexOf(parent);
        if (parentIndex < 0)
            return (true, currentNextSelection);

        if (parent is RootNode rootNode)
            onRootRemoved(rootNode.RootPath);

        var nextSelection = GetNextSibling(parentCollection, parentIndex);
        parentCollection.RemoveAt(parentIndex);
        return (true, nextSelection);
    }

    private static BaseNode? GetNextSibling(IList<BaseNode> siblings, int removedIndex)
    {
        if (siblings.Count <= 1) return null;
        return removedIndex < siblings.Count - 1 ? siblings[removedIndex + 1] : siblings[removedIndex - 1];
    }
}
