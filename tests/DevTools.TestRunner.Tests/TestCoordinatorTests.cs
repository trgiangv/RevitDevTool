using DevTools.Hosting;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.TestRunner.Debugging;
using DevTools.TestRunner.Parsing;
using DevTools.TestRunner.Services;

namespace DevTools.TestRunner.Tests;

[TestClass]
public sealed class TestCoordinatorTests : RunnerTests
{
    [TestMethod]
    public async Task ExecuteAsync_owns_host_pipe_attach_and_request_lifetime()
    {
        var session = new RecordingTestSession(new TestHostPipe("fake-pipe", 4321));
        var debugger = new RecordingDebugger();
        var coordinator = new TestCoordinator(session);
        var parentPid = Environment.ProcessId;
        var context = new RunnerCommandContext(
            "Revit", "2026",
            ForceLaunch: false, PerTestTimeoutSeconds: 60, LaunchTimeoutSeconds: 180,
            Debug: true, DebugParentPid: parentPid);

        var result = await coordinator.ExecuteAsync(
            context,
            debugger,
            (pipe, token) => Task.FromResult($"{pipe.PipeName}:{token.CanBeCanceled}"),
            TestContext.CancellationToken);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual("fake-pipe:True", result.Value);
        Assert.AreEqual(1, session.Calls);
        Assert.AreEqual((4321, (int?)parentPid), debugger.Attached);
    }

    [TestMethod]
    public async Task ExecuteAsync_cancel_does_not_detach()
    {
        var session = new RecordingTestSession(new TestHostPipe("fake-pipe", 4321));
        var debugger = new RecordingDebugger();
        var coordinator = new TestCoordinator(session);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var context = new RunnerCommandContext(
            "Revit", "2026",
            ForceLaunch: false, PerTestTimeoutSeconds: 60, LaunchTimeoutSeconds: 180,
            Debug: true, DebugParentPid: Environment.ProcessId);

        var result = await coordinator.ExecuteAsync(
            context,
            debugger,
            async (_, token) =>
            {
                await cts.CancelAsync();
                token.ThrowIfCancellationRequested();
                return "nope";
            },
            cts.Token);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual((4321, (int?)Environment.ProcessId), debugger.Attached);
    }

    [TestMethod]
    public async Task ExecuteAsync_skips_attach_when_debug_is_disabled()
    {
        var session = new RecordingTestSession(new TestHostPipe("fake-pipe", 4321));
        var debugger = new RecordingDebugger();
        var coordinator = new TestCoordinator(session);
        var context = new RunnerCommandContext(
            "Revit", "2026",
            ForceLaunch: false, PerTestTimeoutSeconds: 60, LaunchTimeoutSeconds: 180,
            Debug: false, DebugParentPid: null);

        var result = await coordinator.ExecuteAsync(
            context,
            debugger,
            static (_, _) => Task.FromResult("ok"),
            TestContext.CancellationToken);

        Assert.IsTrue(result.Succeeded);
        Assert.IsNull(debugger.Attached);
    }

    private sealed class RecordingTestSession(TestHostPipe pipe) : ITestSession
    {
        public int Calls { get; private set; }

        public Task<TestHostPipe> EnsurePipeAsync(HostApp hostApp, string version, bool forceLaunch, TimeSpan launchTimeout, CancellationToken cancellationToken = default)
        {
            Calls++;
            Assert.AreEqual(HostApp.Revit, hostApp);
            Assert.AreEqual("2026", version);
            Assert.IsFalse(forceLaunch);
            Assert.AreEqual(TimeSpan.FromSeconds(180), launchTimeout);
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
