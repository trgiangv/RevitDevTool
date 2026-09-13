using DevTools.Execution.Models;
using DevTools.Execution.Services;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class TreeStateManagerTests
{
    [TestMethod]
    public void CaptureState_PreservesExpansionSelectionAndLastExecuted()
    {
        var manager = new TreeStateManager();
        var executable = ExecutionTestHelpers.CreateExecutableNode("exec://selected");
        executable.IsLastExecuted = true;
        var root = ExecutionTestHelpers.CreateScriptRoot(@"C:\root", executable);
        root.IsExpanded = true;
        executable.IsSelected = true;

        var state = manager.CaptureState([root]);

        Assert.IsTrue(state.ExpandedStates[root.Id]);
        Assert.AreEqual(executable.Id, state.SelectedNodeId);
        Assert.AreEqual(executable.Id, state.LastExecutedNodeId);
    }

    [TestMethod]
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

        Assert.IsTrue(root.IsExpanded);
        Assert.IsTrue(executable.IsSelected);
        Assert.IsTrue(executable.IsLastExecuted);
    }

    [TestMethod]
    public void RestoreState_AutoExpandNew_ExpandsContainersWithChildren()
    {
        var manager = new TreeStateManager();
        var child = ExecutionTestHelpers.CreateExecutableNode("exec://child");
        var root = ExecutionTestHelpers.CreateScriptRoot(@"C:\root", child);
        var state = new TreeState();

        manager.RestoreState([root], state, autoExpandNew: true);

        Assert.IsTrue(root.IsExpanded);
    }
}
