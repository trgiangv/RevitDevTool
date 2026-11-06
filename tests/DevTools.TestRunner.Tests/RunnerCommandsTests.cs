using DevTools.Hosting;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using DevTools.TestRunner.Debugging;
using DevTools.TestRunner.Parsing;
using DevTools.TestRunner.Services;
using System.Text.Json;

namespace DevTools.TestRunner.Tests;

[TestClass]
public sealed class RunnerCommandsTests : RunnerTests
{
    [TestMethod]
    public async Task Missing_assembly_does_not_use_host_session()
    {
        var hosts = new ThrowingTestSession();
        var execute = new TestRunExecute(
            TestingProtocol.Sender,
            new TestHostOptions("Revit", "2026", true, 60, 180),
            new TestRunRequest(
                Guid.NewGuid(),
                TestFrameworkId.NUnit,
                new TestAssemblyReference(Path.Combine(Path.GetTempPath(), "missing-devtools-tests.dll")),
                TestSelection.All));
        var json = JsonSerializer.Serialize(execute, TestingJsonContext.Default.TestRunExecute);
        var commands = new RunnerCommands(
            new TestCoordinator(hosts),
            new ThrowingDebugger(),
            new BufferedRunInput(new StringReader(json)));

        var exitCode = await commands.Run(TestContext.CancellationToken);

        Assert.AreEqual(RunnerExitCode.CliError, exitCode);
        Assert.AreEqual(0, hosts.Calls);
    }

    [TestMethod]
    public async Task Adapter_without_a_sender_is_rejected_before_any_host_work()
    {
        var hosts = new ThrowingTestSession();
        var execute = new TestRunExecute(
            Sender: null,
            new TestHostOptions("Revit", "2026", true, 60, 180),
            new TestRunRequest(
                Guid.NewGuid(),
                TestFrameworkId.NUnit,
                new TestAssemblyReference(Path.Combine(Path.GetTempPath(), "missing-devtools-tests.dll")),
                TestSelection.All));
        var json = JsonSerializer.Serialize(execute, TestingJsonContext.Default.TestRunExecute);
        var commands = new RunnerCommands(
            new TestCoordinator(hosts),
            new ThrowingDebugger(),
            new BufferedRunInput(new StringReader(json)));
        var stderr = new StringWriter();
        var original = Console.Error;
        Console.SetError(stderr);

        int exitCode;
        try
        {
            exitCode = await commands.Run(TestContext.CancellationToken);
        }
        finally
        {
            Console.SetError(original);
        }

        Assert.AreEqual(RunnerExitCode.CliError, exitCode);
        Assert.AreEqual(0, hosts.Calls);
        Assert.Contains(TestingProtocol.Mismatch(execute)!, stderr.ToString(), StringComparison.Ordinal);
    }

    [TestMethod]
    public void Hello_from_a_host_without_a_runtime_range_is_rejected_when_the_output_has_a_manifest()
    {
        var output = Path.Combine(Path.GetTempPath(), $"runner-runtime-{Guid.NewGuid():N}");
        Directory.CreateDirectory(output);
        try
        {
            File.WriteAllText(
                Path.Combine(output, RuntimeManifest.FileName(TestFrameworkId.NUnit)),
                JsonSerializer.Serialize(
                    new RuntimeManifest("NUnit", "Entry", TestingProtocol.RuntimeRange, "nunit.framework", "5.0.0.0"),
                    TestingJsonContext.Default.RuntimeManifest));
            var execute = new TestRunExecute(
                TestingProtocol.Sender,
                new TestHostOptions("Revit", "2026", true, 60, 180),
                new TestRunRequest(
                    Guid.NewGuid(),
                    TestFrameworkId.NUnit,
                    new TestAssemblyReference(Path.Combine(output, "Consumer.dll")),
                    TestSelection.All));
            var hello = new TestHelloResponse(TestFrameworkId.NUnit, "Revit", "2026", 1, false, TestingProtocol.Sender);

            var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
                RunnerCommands.EnsureHelloMatches(hello, execute));

            Assert.EndsWith(TestingProtocol.UpdateProductMessage, error.Message, StringComparison.Ordinal);

            RunnerCommands.EnsureHelloMatches(hello with { TestingRuntime = TestingProtocol.RuntimeRange }, execute);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
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
