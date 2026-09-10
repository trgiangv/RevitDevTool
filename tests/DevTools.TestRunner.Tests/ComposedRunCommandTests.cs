using System.IO.Pipes;
using System.Text.Json;
using DevTools.Hosting;
using DevTools.Ipc;
using DevTools.TestRunner;
using DevTools.TestRunner.Core.Debugging;
using DevTools.TestRunner.Core.Services;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.TestRunner.Tests;

public sealed class ComposedRunCommandTests
{
    private static readonly SemaphoreSlim ConsoleGate = new(1, 1);

    [Fact]
    public async Task Run_sends_testing_run_with_selection_and_attaches_debugger()
    {
        await using var pipe = new FakeHostPipe();
        var hosts = new FakeTestSession(pipe.PipeName);
        var debugger = new FakeDebugger();
        var services = new ServiceCollection();
        services.AddSingleton<ITestSession>(hosts);
        services.AddSingleton<IExecutionCoordinator, ExecutionCoordinator>();
        services.AddSingleton<IDebuggerAttach>(debugger);
        await using var provider = services.BuildServiceProvider();
        var commands = new RunnerCommands(
            provider.GetRequiredService<IExecutionCoordinator>(),
            debugger,
            new BufferedMachineRunInput(TextReader.Null));

        await ConsoleGate.WaitAsync(TestContext.Current.CancellationToken);
        var originalOut = Console.Out;
        using var stdout = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            var exitCode = await commands.Run(
                typeof(ComposedRunCommandTests).Assembly.Location,
                "Revit",
                "2026",
                test: ["Sample.Fixture.PlainTest"],
                debug: true,
                framework: "nunit",
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            ConsoleGate.Release();
        }

        var request = await pipe.RunRequest.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TestingProtocol.Run, request.Method);
        Assert.Contains("Sample.Fixture.PlainTest", request.Filter, StringComparison.Ordinal);
        Assert.Equal(1, hosts.Calls);
        Assert.Equal((1234, (int?)null), debugger.Attached);
        Assert.Contains("framework_id", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MachineRun_sends_the_same_run_id_to_the_host()
    {
        await using var pipe = new FakeHostPipe();
        var hosts = new FakeTestSession(pipe.PipeName);
        var debugger = new FakeDebugger();
        var services = new ServiceCollection();
        services.AddSingleton<ITestSession>(hosts);
        services.AddSingleton<IExecutionCoordinator, ExecutionCoordinator>();
        services.AddSingleton<IDebuggerAttach>(debugger);
        await using var provider = services.BuildServiceProvider();
        var runId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var invocation = new TestingRunInvocation(
            TestingProtocol.CurrentVersion,
            new TestingHostOptions("Revit", "2026", false, 60, 180, null),
            new TestingRunRequest(
                TestingProtocol.CurrentVersion,
                runId,
                "nunit",
                new TestingAssemblyReference(typeof(ComposedRunCommandTests).Assembly.Location),
                TestingSelection.FromTestIds(["Sample.Fixture.PlainTest"])));
        var json = JsonSerializer.Serialize(invocation, TestingJsonContext.Default.TestingRunInvocation);
        var commands = new RunnerCommands(
            provider.GetRequiredService<IExecutionCoordinator>(),
            debugger,
            new BufferedMachineRunInput(new StringReader(json)));

        await ConsoleGate.WaitAsync(TestContext.Current.CancellationToken);
        var originalOut = Console.Out;
        using var stdout = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            var exitCode = await commands.MachineRun(TestContext.Current.CancellationToken);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            ConsoleGate.Release();
        }

        var request = await pipe.RunRequest.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TestingProtocol.Run, request.Method);
        Assert.Equal(runId, request.RunId);
        Assert.Contains("Sample.Fixture.PlainTest", request.Filter, StringComparison.Ordinal);
        Assert.Equal(1, hosts.Calls);
    }

    private sealed class FakeTestSession(string pipeName) : ITestSession
    {
        public int Calls { get; private set; }

        public Task<HostPipeInstance> EnsurePipeAsync(HostApp hostApp, string version, bool forceLaunch, TimeSpan launchTimeout, CancellationToken cancellationToken = default)
        {
            Calls++;
            Assert.Equal(HostApp.Revit, hostApp);
            Assert.Equal("2026", version);
            Assert.False(forceLaunch);
            return Task.FromResult(new HostPipeInstance(pipeName, 1234));
        }
    }

    private sealed class FakeDebugger : IDebuggerAttach
    {
        public (int HostPid, int? ParentPid)? Attached { get; private set; }

        public bool TryAttach(AttachTarget target, TextWriter warnings)
        {
            Attached = (target.HostProcessId, target.ParentProcessId);
            return true;
        }
    }

    private sealed record WireRequest(
        string Method,
        string Filter,
        Guid RunId);

    private sealed class FakeHostPipe : IAsyncDisposable
    {
        private readonly NamedPipeServerStream pipe;
        private readonly CancellationTokenSource cancellation = new();
        private readonly Task serving;

        public FakeHostPipe()
        {
            PipeName = $"devtools-task5-{Guid.NewGuid():N}";
            pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            serving = ServeAsync();
        }

        public string PipeName { get; }
        public TaskCompletionSource<WireRequest> RunRequest { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private async Task ServeAsync()
        {
            try
            {
                await pipe.WaitForConnectionAsync(cancellation.Token);
                using var connection = new BridgePipeConnection(pipe);
                connection.MessageReceived += message => _ = RespondAsync(connection, message);
                connection.StartReadLoop();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
        }

        private async Task RespondAsync(BridgePipeConnection connection, BridgeMessage request)
        {
            if (request.Type != BridgeMessage.TypeRequest || request.Id is null)
                return;

            if (request.Method == TestingProtocol.Hello)
            {
                await connection.WriteAsync(BridgeMessage.Response(
                    request.Id,
                    JsonSerializer.SerializeToElement(
                        new TestingHelloResponse(TestingProtocol.CurrentVersion, "nunit", "Revit", "2026", 1234, false),
                        TestingJsonContext.Default.TestingHelloResponse)), cancellation.Token);
                return;
            }
            if (request.Method == TestingProtocol.Run)
            {
                var run = request.Params!.Value.Deserialize(TestingJsonContext.Default.TestingRunRequest)!;
                await connection.WriteAsync(BridgeMessage.Response(
                    request.Id,
                    JsonSerializer.SerializeToElement(
                        new TestingRunResponse(run.RunId, "nunit", "generation", [], TestingCancellationState.None, null, null),
                        TestingJsonContext.Default.TestingRunResponse)), cancellation.Token);
                RunRequest.TrySetResult(new WireRequest(
                    request.Method,
                    string.Join(",", run.Selection.TestIds.Concat(
                        string.IsNullOrWhiteSpace(run.Selection.FilterData)
                            ? []
                            : [run.Selection.FilterData])),
                    run.RunId));
                return;
            }
        }

        public async ValueTask DisposeAsync()
        {
            cancellation.Cancel();
            pipe.Dispose();
            try { await serving; } catch (ObjectDisposedException) { }
            cancellation.Dispose();
        }
    }
}
