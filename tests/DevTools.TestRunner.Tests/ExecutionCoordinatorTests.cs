using DevTools.Hosting;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.TestRunner.Debugging;
using DevTools.TestRunner.Parsing;
using DevTools.TestRunner.Services;

namespace DevTools.TestRunner.Tests;

public sealed class ExecutionCoordinatorTests
{
    [Fact]
    public async Task ExecuteAsync_owns_host_pipe_attach_and_request_lifetime()
    {
        var session = new RecordingTestSession(new HostPipeInstance("fake-pipe", 4321));
        var debugger = new RecordingDebugger();
        var coordinator = new ExecutionCoordinator(session);
        var parentPid = Environment.ProcessId;
        var context = new RunnerCommandContext(
            typeof(ExecutionCoordinatorTests).Assembly.Location, "Revit", "2026",
            ForceLaunch: false, PerTestTimeoutSeconds: 60, LaunchTimeoutSeconds: 180,
            Debug: true, DebugParentPid: parentPid, FrameworkId: TestFrameworkId.NUnit);

        var result = await coordinator.ExecuteAsync(
            context,
            debugger,
            (pipe, cancellationToken) => Task.FromResult($"{pipe.PipeName}:{cancellationToken.CanBeCanceled}"),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("fake-pipe:True", result.Value);
        Assert.Equal(1, session.Calls);
        Assert.Equal((4321, parentPid), debugger.Attached);
    }

    [Fact]
    public async Task ExecuteAsync_skips_attach_when_debug_is_disabled()
    {
        var session = new RecordingTestSession(new HostPipeInstance("fake-pipe", 4321));
        var debugger = new RecordingDebugger();
        var coordinator = new ExecutionCoordinator(session);
        var context = new RunnerCommandContext(
            typeof(ExecutionCoordinatorTests).Assembly.Location, "Revit", "2026",
            ForceLaunch: false, PerTestTimeoutSeconds: 60, LaunchTimeoutSeconds: 180,
            Debug: false, DebugParentPid: null, FrameworkId: TestFrameworkId.NUnit);

        var result = await coordinator.ExecuteAsync(
            context,
            debugger,
            static (_, _) => Task.FromResult("ok"),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Null(debugger.Attached);
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

    private sealed class RecordingDebugger : IDebuggerAttach
    {
        public (int HostPid, int? ParentPid)? Attached { get; private set; }

        public bool TryAttach(AttachTarget target, TextWriter warnings)
        {
            Attached = (target.HostProcessId, target.ParentProcessId);
            return true;
        }
    }
}
