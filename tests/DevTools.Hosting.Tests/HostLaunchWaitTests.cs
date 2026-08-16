using System.Diagnostics;
using DevTools.Hosting;

namespace DevTools.Hosting.Tests;

public sealed class HostLaunchWaitTests
{
    [Fact]
    public async Task UntilAsync_returns_Ready_when_probe_succeeds()
    {
        var status = await HostLaunchWait.UntilAsync(
            Process.GetCurrentProcess(),
            static () => true,
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        Assert.Equal(HostReadyStatus.Ready, status);
    }

    [Fact]
    public async Task UntilAsync_returns_Exited_when_process_has_exited()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c exit 0",
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        Assert.True(process.WaitForExit(5000));

        var status = await HostLaunchWait.UntilAsync(
            process,
            static () => false,
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        Assert.Equal(HostReadyStatus.Exited, status);
    }

    [Fact]
    public async Task UntilAsync_returns_TimedOut_when_probe_never_succeeds()
    {
        var status = await HostLaunchWait.UntilAsync(
            Process.GetCurrentProcess(),
            static () => false,
            TimeSpan.FromMilliseconds(40),
            TestContext.Current.CancellationToken,
            pollInterval: TimeSpan.FromMilliseconds(10));

        Assert.Equal(HostReadyStatus.TimedOut, status);
    }

    [Fact]
    public async Task UntilAsync_returns_Cancelled_when_token_is_cancelled()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        var status = await HostLaunchWait.UntilAsync(
            Process.GetCurrentProcess(),
            static () => false,
            TimeSpan.FromSeconds(5),
            cts.Token);

        Assert.Equal(HostReadyStatus.Cancelled, status);
    }
}
