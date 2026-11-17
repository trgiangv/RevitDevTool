using DevTools.Hosting;
using DevTools.TestRunner.Services;
using System.Diagnostics;

namespace DevTools.TestRunner.Tests;

[TestClass]
public sealed class TestSessionTests : RunnerTests
{
    [TestMethod]
    public async Task EnsurePipeAsync_kills_spawned_process_when_launch_is_cancelled()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c ping -t 127.0.0.1",
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        var session = new TestSession(new StubLaunchService(process));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var wait = session.EnsurePipeAsync(
            HostApp.Revit,
            "2025",
            forceLaunch: true,
            hostPid: null,
            TimeSpan.FromSeconds(30),
            cts.Token);

        await Task.Delay(200, TestContext.CancellationToken);
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await wait);
        Assert.IsTrue(process.WaitForExit(5000));
    }

    [TestMethod]
    public void NoPidReusesOldestHost()
    {
        var picked = TestSession.Select(Hosts, HostApp.Revit, "2026", hostPid: null);

        Assert.AreEqual(1200, picked?.ProcessId);
    }

    [TestMethod]
    public void PidPicksThatHost()
    {
        var picked = TestSession.Select(Hosts, HostApp.Revit, "2026", hostPid: 3400);

        Assert.AreEqual("DevTools_Revit_2026_3400", picked?.PipeName);
    }

    [TestMethod]
    public void UnknownPidStopsTheRun()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            TestSession.Select(Hosts, HostApp.Revit, "2026", hostPid: 999));

        Assert.AreEqual("Process 999 is not a running Revit 2026 with the RevitDevTool test pipe.", error.Message);
    }

    [TestMethod]
    public void PidWithNoHostsStopsTheRun()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TestSession.Select([], HostApp.Revit, "2026", hostPid: 3400));
    }

    [TestMethod]
    public void NoHostsAndNoPidLaunches()
    {
        Assert.IsNull(TestSession.Select([], HostApp.Revit, "2026", hostPid: null));
    }

    private static readonly TestHostPipe[] Hosts =
    [
        new("DevTools_Revit_2026_3400", 3400),
        new("DevTools_Revit_2026_1200", 1200),
    ];

    private sealed class StubLaunchService(Process process) : IHostLaunchService
    {
        public HostProcessStart Start(HostLaunchRequest request, CancellationToken cancellationToken) =>
            new(process, request.Version, "cmd.exe", null, [], null);
    }
}
