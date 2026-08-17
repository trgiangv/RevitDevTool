using DevTools.Hosting;
using DevTools.TestRunner.Core.Debugging;
using DevTools.TestRunner.Core.Parsing;
using DevTools.TestRunner.Core.Services;

namespace DevTools.TestRunner.Core.Tests;

public sealed class HostExecutionCoordinatorTests
{
    [Fact]
    public async Task ExecuteAsync_owns_host_pipe_debug_scope_and_request_lifetime()
    {
        var hosts = new RecordingHostSession(new HostPipeInstance("fake-pipe", 4321));
        var debugger = new RecordingDebugger();
        var coordinator = new HostExecutionCoordinator(hosts);
        var context = new RunnerCommandContext(
            "run", typeof(HostExecutionCoordinatorTests).Assembly.Location, "Revit", "2026",
            HostLaunch: false, HostTimeoutSeconds: 60, HostLaunchTimeoutSeconds: 180,
            Debug: true, DebugParentPid: 99, FrameworkId: "example", UseGenericProtocol: true);

        var result = await coordinator.ExecuteAsync(
            context,
            debugger,
            (pipe, cancellationToken) => Task.FromResult($"{pipe.PipeName}:{cancellationToken.CanBeCanceled}"),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("fake-pipe:True", result.Value);
        Assert.Equal(1, hosts.Calls);
        Assert.Equal((4321, 99), debugger.Attached);
        Assert.Equal(4321, debugger.DetachedProcessId);
    }

    private sealed class RecordingHostSession(HostPipeInstance pipe) : IHostSession
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

    private sealed class RecordingDebugger : IVisualStudioAttach
    {
        public (int HostPid, int? ParentPid)? Attached { get; private set; }
        public int? DetachedProcessId { get; private set; }

        public bool TryAttach(int hostProcessId, int? parentProcessId, TextWriter warnings)
        {
            Attached = (hostProcessId, parentProcessId);
            return true;
        }

        public void TryDetach(int hostProcessId, TextWriter warnings) => DetachedProcessId = hostProcessId;
    }
}
