using System.Diagnostics;
using DevTools.Hosting;
using DevTools.TestRunner.Core.Debugging;
using DevTools.TestRunner.Core.Parsing;
using DevTools.TestRunner.Core.Services;

namespace DevTools.TestRunner.Core.Tests;

public sealed class ExecutionCoordinatorTests
{
    [Fact]
    public async Task ExecuteAsync_owns_host_pipe_debug_scope_and_request_lifetime()
    {
        var session = new RecordingTestSession(new HostPipeInstance("fake-pipe", 4321));
        var debugger = new RecordingDebugger();
        var coordinator = new ExecutionCoordinator(session);
        var parentPid = Environment.ProcessId;
        var context = new RunnerCommandContext(
            typeof(ExecutionCoordinatorTests).Assembly.Location, "Revit", "2026",
            ForceLaunch: false, PerTestTimeoutSeconds: 60, LaunchTimeoutSeconds: 180,
            Debug: true, DebugParentPid: parentPid, FrameworkId: "example");

        var result = await coordinator.ExecuteAsync(
            context,
            debugger,
            (pipe, cancellationToken) => Task.FromResult($"{pipe.PipeName}:{cancellationToken.CanBeCanceled}"),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("fake-pipe:True", result.Value);
        Assert.Equal(1, session.Calls);
        Assert.Equal((4321, parentPid), debugger.Attached);
        Assert.Equal(4321, debugger.DetachedProcessId);
    }

    [Fact]
    public async Task ExecuteAsync_cancels_host_wait_when_debug_parent_exits()
    {
        using var parent = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c ping -t 127.0.0.1",
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        var session = new CancelObservingSession();
        var coordinator = new ExecutionCoordinator(session);
        var context = new RunnerCommandContext(
            typeof(ExecutionCoordinatorTests).Assembly.Location, "Revit", "2026",
            ForceLaunch: false, PerTestTimeoutSeconds: 60, LaunchTimeoutSeconds: 180,
            Debug: true, DebugParentPid: parent.Id, FrameworkId: "example");

        var execute = coordinator.ExecuteAsync(
            context,
            new RecordingDebugger(),
            static (_, _) => Task.FromResult("unused"),
            TestContext.Current.CancellationToken);

        await Task.Delay(100, TestContext.Current.CancellationToken);
        parent.Kill(entireProcessTree: true);
        var result = await execute;

        Assert.False(result.Succeeded);
        Assert.Equal(ExecutionFailure.NoHost, result.Failure);
        Assert.True(session.SawCancellation);
    }

    private sealed class RecordingTestSession(HostPipeInstance pipe) : ITestSession
    {
        public int Calls { get; private set; }

        public Task<HostPipeInstance> EnsurePipeAsync(HostApp hostApp, string version, bool forceLaunch, TimeSpan launchTimeout, CancellationToken cancellationToken = default)
        {
            Calls++;
            Assert.Equal(HostApp.Revit, hostApp);
            Assert.Equal("2026", version);
            Assert.False(forceLaunch);
            Assert.Equal(TimeSpan.FromSeconds(180), launchTimeout);
            return Task.FromResult(pipe);
        }
    }

    private sealed class CancelObservingSession : ITestSession
    {
        public bool SawCancellation { get; private set; }

        public async Task<HostPipeInstance> EnsurePipeAsync(
            HostApp hostApp,
            string version,
            bool forceLaunch,
            TimeSpan launchTimeout,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                SawCancellation = true;
                throw;
            }

            return new HostPipeInstance("unused", 1);
        }
    }

    private sealed class RecordingDebugger : IDebuggerAttach
    {
        public (int HostPid, int? ParentPid)? Attached { get; private set; }
        public int? DetachedProcessId { get; private set; }

        public bool TryAttach(AttachTarget target, TextWriter warnings)
        {
            Attached = (target.HostProcessId, target.ParentProcessId);
            return true;
        }

        public void TryDetach(int hostProcessId, TextWriter warnings) => DetachedProcessId = hostProcessId;
    }
}
