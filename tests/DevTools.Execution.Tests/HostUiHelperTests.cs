using DevTools.UI;

namespace DevTools.Execution.Tests;

public sealed class HostUiHelperTests
{
    [Fact]
    public void RunOnMainThread_WhenDispatcherIsNull_RunsInline()
    {
        if (HostUiHelper.HostDispatcher is not null)
            Assert.Skip("Host dispatcher already initialized in this process.");

        var ran = false;
        HostUiHelper.RunOnMainThread(() => ran = true);
        Assert.True(ran);
    }

    [Fact]
    public void RunOnMainThread_WhenActionIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => HostUiHelper.RunOnMainThread(null!));
    }
}
