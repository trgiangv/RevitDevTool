using System.Collections.ObjectModel;
using DevTools.Execution.Models;
using DevTools.Execution.Services;

namespace DevTools.Execution.Tests;

public sealed class TreeNodeOperationsTests
{
    [Fact]
    public void MergeNodesIntoTree_AddsNewRootAndMergesChildren()
    {
        var tree = new ObservableCollection<ExecutionNodeBase>();
        var existing = ExecutionTestHelpers.CreateScriptRoot(
            @"C:\root",
            ExecutionTestHelpers.CreateExecutableNode("exec://a"));
        tree.Add(existing);

        var incoming = ExecutionTestHelpers.CreateScriptRoot(
            @"C:\root",
            ExecutionTestHelpers.CreateExecutableNode("exec://a"),
            ExecutionTestHelpers.CreateExecutableNode("exec://b"));

        TreeNodeOperations.MergeNodesIntoTree(tree, [incoming]);

        Assert.Single(tree);
        Assert.Equal(2, tree[0].Children.Count);
    }

    [Fact]
    public void CollectExecutableIdSet_ReturnsAllExecutableIds()
    {
        var root = ExecutionTestHelpers.CreateScriptRoot(
            @"C:\root",
            ExecutionTestHelpers.CreateExecutableNode("exec://one"),
            ExecutionTestHelpers.CreateExecutableNode("exec://two"));

        var ids = TreeNodeOperations.CollectExecutableIdSet([root]);

        Assert.Equal(2, ids.Count);
        Assert.Contains("exec://one", ids);
        Assert.Contains("exec://two", ids);
    }

    [Fact]
    public void PromoteLatestNewExecutable_MarksLastNewNode()
    {
        var previous = new HashSet<string>(StringComparer.Ordinal) { "exec://old" };
        var root = ExecutionTestHelpers.CreateScriptRoot(
            @"C:\root",
            ExecutionTestHelpers.CreateExecutableNode("exec://old"),
            ExecutionTestHelpers.CreateExecutableNode("exec://new"));

        var promoted = TreeNodeOperations.PromoteLatestNewExecutable([root], previous);

        Assert.NotNull(promoted);
        Assert.Equal("exec://new", promoted!.Id);
        Assert.True(promoted.IsLastExecuted);
    }

    [Fact]
    public void RemoveNodeWithCascade_RemovesRootAndReturnsSibling()
    {
        var tree = new ObservableCollection<ExecutionNodeBase>();
        var first = ExecutionTestHelpers.CreateScriptRoot(@"C:\first");
        var second = ExecutionTestHelpers.CreateScriptRoot(@"C:\second");
        tree.Add(first);
        tree.Add(second);
        var unwatchPaths = new List<string>();

        var result = TreeNodeOperations.RemoveNodeWithCascade(tree, first, unwatchPaths.Add);

        Assert.True(result.Removed);
        Assert.Same(second, result.NextSelection);
        Assert.Single(tree);
        Assert.Equal(@"C:\first", Assert.Single(unwatchPaths));
    }

    [Fact]
    public void RemoveNodeWithCascade_RemovesNestedExecutable()
    {
        var tree = new ObservableCollection<ExecutionNodeBase>();
        var executable = ExecutionTestHelpers.CreateExecutableNode("exec://nested");
        var root = ExecutionTestHelpers.CreateScriptRoot(@"C:\root", executable);
        tree.Add(root);

        var result = TreeNodeOperations.RemoveNodeWithCascade(tree, executable, _ => { });

        Assert.True(result.Removed);
        Assert.Empty(root.Children);
    }

    [Fact]
    public void PromoteLatestNewExecutable_ReturnsNull_WhenNoNewExecutables()
    {
        var root = ExecutionTestHelpers.CreateScriptRoot(
            @"C:\root",
            ExecutionTestHelpers.CreateExecutableNode("exec://same"));
        var previous = TreeNodeOperations.CollectExecutableIdSet([root]);

        Assert.Null(TreeNodeOperations.PromoteLatestNewExecutable([root], previous));
    }

    [Fact]
    public void RemoveNodeWithCascade_ReturnsFalse_WhenNodeMissing()
    {
        var tree = new ObservableCollection<ExecutionNodeBase>();
        var orphan = ExecutionTestHelpers.CreateExecutableNode("exec://missing");

        var result = TreeNodeOperations.RemoveNodeWithCascade(tree, orphan, _ => { });

        Assert.False(result.Removed);
        Assert.Null(result.NextSelection);
    }

    [Fact]
    public void ReplaceRootSnapshot_ReplacesEntireTree()
    {
        var tree = new ObservableCollection<ExecutionNodeBase> { ExecutionTestHelpers.CreateScriptRoot(@"C:\old") };
        var replacement = ExecutionTestHelpers.CreateScriptRoot(@"C:\new");

        TreeNodeOperations.ReplaceRootSnapshot(tree, [replacement]);

        Assert.Single(tree);
        Assert.Equal(@"C:\new", ((ExecutionNodeRoot)tree[0]).RootPath);
    }
}
