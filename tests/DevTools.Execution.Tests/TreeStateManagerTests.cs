using DevTools.Execution.Models;
using DevTools.Execution.Services;

namespace DevTools.Execution.Tests;

public sealed class TreeStateManagerTests
{
    [Fact]
    public void CaptureState_PreservesExpansionSelectionAndLastExecuted()
    {
        var manager = new TreeStateManager();
        var executable = ExecutionTestHelpers.CreateExecutableNode("exec://selected");
        executable.IsLastExecuted = true;
        var root = ExecutionTestHelpers.CreateScriptRoot(@"C:\root", executable);
        root.IsExpanded = true;
        executable.IsSelected = true;

        var state = manager.CaptureState([root]);

        Assert.True(state.ExpandedStates[root.Id]);
        Assert.Equal(executable.Id, state.SelectedNodeId);
        Assert.Equal(executable.Id, state.LastExecutedNodeId);
    }

    [Fact]
    public void RestoreState_RestoresCapturedFlags()
    {
        var manager = new TreeStateManager();
        var executable = ExecutionTestHelpers.CreateExecutableNode("exec://run");
        var root = ExecutionTestHelpers.CreateScriptRoot(@"C:\root", executable);
        root.IsExpanded = true;
        executable.IsSelected = true;
        executable.IsLastExecuted = true;
        var state = manager.CaptureState([root]);

        root.IsExpanded = false;
        executable.IsSelected = false;
        executable.IsLastExecuted = false;

        manager.RestoreState([root], state);

        Assert.True(root.IsExpanded);
        Assert.True(executable.IsSelected);
        Assert.True(executable.IsLastExecuted);
    }

    [Fact]
    public void RestoreState_AutoExpandNew_ExpandsContainersWithChildren()
    {
        var manager = new TreeStateManager();
        var child = ExecutionTestHelpers.CreateExecutableNode("exec://child");
        var root = ExecutionTestHelpers.CreateScriptRoot(@"C:\root", child);
        var state = new TreeState();

        manager.RestoreState([root], state, autoExpandNew: true);

        Assert.True(root.IsExpanded);
    }
}
