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
}
