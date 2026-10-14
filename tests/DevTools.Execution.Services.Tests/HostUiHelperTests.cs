using DevTools.UI;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class HostUiHelperTests
{
    [TestMethod]
    public void RunOnMainThread_WhenDispatcherIsNull_RunsInline()
    {
        if (HostUiHelper.HostDispatcher is not null)
            Assert.Inconclusive("Host dispatcher already initialized in this process.");

        var ran = false;
        HostUiHelper.RunOnMainThread(() => ran = true);
        Assert.IsTrue(ran);
    }

    [TestMethod]
    public void RunOnMainThread_WhenActionIsNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => HostUiHelper.RunOnMainThread(null!));
    }

    [TestMethod]
    public void RequireCompleted_WhenTaskCompleted_Returns()
    {
        HostUiHelper.RequireCompleted(Task.CompletedTask);
    }

    [TestMethod]
    public void RequireCompleted_WhenTaskFaulted_Rethrows()
    {
        var failed = Task.FromException(new InvalidOperationException("start failed"));
        Assert.ThrowsExactly<InvalidOperationException>(() => HostUiHelper.RequireCompleted(failed));
    }

    [TestMethod]
    public void RequireCompleted_WhenTaskIncomplete_Throws()
    {
        var pending = new TaskCompletionSource();
        Assert.ThrowsExactly<InvalidOperationException>(() => HostUiHelper.RequireCompleted(pending.Task));
    }
}
