using System.Text.Json;
using DevTools.Hosting;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using DevTools.TestRunner.Debugging;
using DevTools.TestRunner.Parsing;
using DevTools.TestRunner.Services;

namespace DevTools.TestRunner.Tests;

public sealed class RunnerCommandsTests
{
    [Fact]
    public async Task Missing_assembly_does_not_use_host_session()
    {
        var hosts = new ThrowingTestSession();
        var execute = new TestRunExecute(
            TestingProtocol.CurrentVersion,
            new TestHostOptions("Revit", "2026", true, 60, 180),
            new TestRunRequest(
                TestingProtocol.CurrentVersion,
                Guid.NewGuid(),
                TestFrameworkId.NUnit,
                new TestAssemblyReference(Path.Combine(Path.GetTempPath(), "missing-devtools-tests.dll")),
                TestSelection.All));
        var json = JsonSerializer.Serialize(execute, TestingJsonContext.Default.TestRunExecute);
        var commands = new RunnerCommands(
            new TestCoordinator(hosts),
            new ThrowingDebugger(),
            new BufferedRunInput(new StringReader(json)));

        var exitCode = await commands.Run(TestContext.Current.CancellationToken);

        Assert.Equal(RunnerExitCode.CliError, exitCode);
        Assert.Equal(0, hosts.Calls);
    }

    private sealed class ThrowingTestSession : ITestSession
    {
        public int Calls { get; private set; }

        public Task<TestHostPipe> EnsurePipeAsync(HostApp hostApp, string version, bool forceLaunch, TimeSpan launchTimeout, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("A missing assembly must not activate a host.");
        }
    }

    private sealed class ThrowingDebugger : IDebuggerAttach
    {
        public bool TryAttach(AttachTarget target, TextWriter warnings) =>
            throw new InvalidOperationException("A missing assembly must not attach a debugger.");
    }
}
