using System.Diagnostics;
using DevTools.Hosting;
using DevTools.TestRunner.Core.Services;

namespace DevTools.TestRunner.Core.Tests;

public sealed class TestSessionTests
{
    [Fact]
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
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var wait = session.EnsurePipeAsync(
            HostApp.Revit,
            "2025",
            forceLaunch: true,
            TimeSpan.FromSeconds(30),
            cts.Token);

        await Task.Delay(200, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        Assert.True(process.WaitForExit(5000));
    }

    private sealed class StubLaunchService(Process process) : IHostLaunchService
    {
        public HostProcessStart Start(HostLaunchRequest request, CancellationToken cancellationToken) =>
            new(process, request.Version, "cmd.exe", null, [], null);
    }
}
