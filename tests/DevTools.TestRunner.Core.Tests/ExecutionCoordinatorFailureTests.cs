using DevTools.Hosting;
using DevTools.TestRunner.Core.Debugging;
using DevTools.TestRunner.Core.Parsing;
using DevTools.TestRunner.Core.Services;

namespace DevTools.TestRunner.Core.Tests;

public sealed class ExecutionCoordinatorFailureTests
{
    [Fact]
    public async Task ExecuteAsync_returns_invalid_host_for_unknown_host_name()
    {
        var coordinator = new ExecutionCoordinator(new ThrowingTestSession());
        var context = new RunnerCommandContext(
            typeof(ExecutionCoordinatorFailureTests).Assembly.Location,
            "UnknownHost",
            "2026",
            ForceLaunch: false,
            PerTestTimeoutSeconds: 60,
            LaunchTimeoutSeconds: 180,
            Debug: false,
            DebugParentPid: null,
            FrameworkId: "example");

        var result = await coordinator.ExecuteAsync(
            context,
            new RecordingDebugger(),
            static (_, _) => Task.FromResult("unused"),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ExecutionFailure.InvalidHost, result.Failure);
        Assert.Contains("Unsupported host", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_maps_session_failures_to_no_host()
    {
        var coordinator = new ExecutionCoordinator(new ThrowingTestSession());
        var context = new RunnerCommandContext(
            typeof(ExecutionCoordinatorFailureTests).Assembly.Location,
            "Revit",
            "2026",
            ForceLaunch: false,
            PerTestTimeoutSeconds: 60,
            LaunchTimeoutSeconds: 180,
            Debug: false,
            DebugParentPid: null,
            FrameworkId: "example");

        var result = await coordinator.ExecuteAsync(
            context,
            new RecordingDebugger(),
            static (_, _) => Task.FromResult("unused"),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(ExecutionFailure.NoHost, result.Failure);
        Assert.Equal("pipe unavailable", result.Error);
    }

    private sealed class ThrowingTestSession : ITestSession
    {
        public Task<HostPipeInstance> EnsurePipeAsync(
            HostApp hostApp,
            string version,
            bool forceLaunch,
            TimeSpan launchTimeout,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("pipe unavailable");
    }

    private sealed class RecordingDebugger : IDebuggerAttach
    {
        public bool TryAttach(AttachTarget target, TextWriter warnings) => true;
        public void TryDetach(int hostProcessId, TextWriter warnings) { }
    }
}
