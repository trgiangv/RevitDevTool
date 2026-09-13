using DevTools.Hosting;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.TestRunner.Debugging;
using DevTools.TestRunner.Parsing;
using DevTools.TestRunner.Services;

namespace DevTools.TestRunner.Tests;

[TestClass]
public sealed class TestCoordinatorFailureTests : RunnerTests
{
    [TestMethod]
    public async Task ExecuteAsync_returns_invalid_host_for_unknown_host_name()
    {
        var coordinator = new TestCoordinator(new ThrowingTestSession());
        var context = new RunnerCommandContext(
            "UnknownHost",
            "2026",
            ForceLaunch: false,
            PerTestTimeoutSeconds: 60,
            LaunchTimeoutSeconds: 180,
            Debug: false,
            DebugParentPid: null);

        var result = await coordinator.ExecuteAsync(
            context,
            new NoOpDebugger(),
            static (_, _) => Task.FromResult("unused"),
            TestContext.CancellationToken);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(ExecutionFailure.InvalidHost, result.Failure);
        Assert.IsNotNull(result.Error);
        Assert.Contains("Unsupported host", result.Error, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ExecuteAsync_maps_session_failures_to_no_host()
    {
        var coordinator = new TestCoordinator(new ThrowingTestSession());
        var context = new RunnerCommandContext(
            "Revit",
            "2026",
            ForceLaunch: false,
            PerTestTimeoutSeconds: 60,
            LaunchTimeoutSeconds: 180,
            Debug: false,
            DebugParentPid: null);

        var result = await coordinator.ExecuteAsync(
            context,
            new NoOpDebugger(),
            static (_, _) => Task.FromResult("unused"),
            TestContext.CancellationToken);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(ExecutionFailure.NoHost, result.Failure);
        Assert.AreEqual("pipe unavailable", result.Error);
    }

    private sealed class ThrowingTestSession : ITestSession
    {
        public Task<TestHostPipe> EnsurePipeAsync(
            HostApp hostApp,
            string version,
            bool forceLaunch,
            TimeSpan launchTimeout,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("pipe unavailable");
    }

    private sealed class NoOpDebugger : IDebuggerAttach
    {
        public bool TryAttach(AttachTarget target, TextWriter warnings) => false;
    }
}
