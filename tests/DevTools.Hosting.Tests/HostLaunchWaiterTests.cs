using System.Diagnostics;
using DevTools.Hosting;

namespace DevTools.Hosting.Tests;

[TestClass]
public sealed class HostLaunchWaiterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task UntilAsync_returns_Ready_when_probe_succeeds()
    {
        var status = await HostLaunchWaiter.UntilAsync(
            Process.GetCurrentProcess(),
            static () => true,
            TimeSpan.FromSeconds(2),
            TestContext.CancellationToken);

        Assert.AreEqual(HostStatus.Ready, status);
    }

    [TestMethod]
    public async Task UntilAsync_returns_Exited_when_process_has_exited()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c exit 0",
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        Assert.IsTrue(process.WaitForExit(5000));

        var status = await HostLaunchWaiter.UntilAsync(
            process,
            static () => false,
            TimeSpan.FromSeconds(2),
            TestContext.CancellationToken);

        Assert.AreEqual(HostStatus.Exited, status);
    }

    [TestMethod]
    public async Task UntilAsync_returns_TimedOut_when_probe_never_succeeds()
    {
        var status = await HostLaunchWaiter.UntilAsync(
            Process.GetCurrentProcess(),
            static () => false,
            TimeSpan.FromMilliseconds(40),
            TestContext.CancellationToken,
            pollInterval: TimeSpan.FromMilliseconds(10));

        Assert.AreEqual(HostStatus.TimedOut, status);
    }

    [TestMethod]
    public async Task UntilAsync_returns_Cancelled_when_token_is_cancelled()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        await cts.CancelAsync();

        var status = await HostLaunchWaiter.UntilAsync(
            Process.GetCurrentProcess(),
            static () => false,
            TimeSpan.FromSeconds(5),
            cts.Token);

        Assert.AreEqual(HostStatus.Cancelled, status);
    }

    [TestMethod]
    public void TerminateIfIncomplete_kills_on_cancelled()
    {
        using var process = StartLongLived();
        HostLaunchWaiter.TerminateIfIncomplete(process, HostStatus.Cancelled);
        Assert.IsTrue(process.WaitForExit(5000));
    }

    [TestMethod]
    public void TerminateIfIncomplete_leaves_current_process_on_ready()
    {
        var process = Process.GetCurrentProcess();
        HostLaunchWaiter.TerminateIfIncomplete(process, HostStatus.Ready);
        Assert.IsFalse(process.HasExited);
    }

    private static Process StartLongLived() =>
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c ping -t 127.0.0.1",
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
}
