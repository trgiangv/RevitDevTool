using DevTools.Hosting;
using DevTools.TestRunner;
using DevTools.TestRunner.Core.Debugging;
using DevTools.TestRunner.Core.Parsing;
using DevTools.TestRunner.Core.Services;

namespace DevTools.TestRunner.Tests;

public sealed class RunnerCommandsTests
{
    [Fact]
    public async Task Missing_assembly_does_not_use_host_session()
    {
        var hosts = new ThrowingTestSession();
        var commands = new RunnerCommands(
            new ExecutionCoordinator(hosts),
            new ThrowingDebugger());

        var exitCode = await commands.Run(
            Path.Combine(Path.GetTempPath(), "missing-devtools-tests.dll"),
            "Revit",
            "2026",
            forceLaunch: true,
            perTestTimeout: 60,
            launchTimeout: 180,
            framework: "nunit",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(RunnerExitCode.CliError, exitCode);
        Assert.Equal(0, hosts.Calls);
    }

    private sealed class ThrowingTestSession : ITestSession
    {
        public int Calls { get; private set; }

        public Task<HostPipeInstance> EnsurePipeAsync(HostApp hostApp, string version, bool forceLaunch, TimeSpan launchTimeout, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("A missing assembly must not activate a host.");
        }
    }

    private sealed class ThrowingDebugger : IDebuggerAttach
    {
        public bool TryAttach(AttachTarget target, TextWriter warnings) =>
            throw new InvalidOperationException("A missing assembly must not attach a debugger.");

        public void TryDetach(int hostProcessId, TextWriter warnings) =>
            throw new InvalidOperationException("A missing assembly must not detach a debugger.");
    }
}
