using System.IO.Pipes;
using System.Text.Json;
using DevTools.Hosting;
using DevTools.Ipc;
using DevTools.NUnit.Provider;
using DevTools.NUnit.Runner;
using DevTools.TestRunner.Core.Composition;
using DevTools.TestRunner.Core.Debugging;
using DevTools.TestRunner.Core.Services;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.NUnit.Runner.Tests;

public sealed class ComposedRunCommandTests
{
    private static readonly SemaphoreSlim ConsoleGate = new(1, 1);

    [Fact]
    public async Task Run_uses_registry_selected_nunit_module_and_provider_filter_without_host_contact()
    {
        await using var pipe = new FakeHostPipe();
        var hosts = new FakeHostSession(pipe.PipeName);
        var debugger = new FakeDebugger();
        var modules = new RunnerModuleRegistry();
        modules.Register(new NUnitRunnerModule(), isDefault: true);
        var services = new ServiceCollection();
        services.AddSingleton<IHostSession>(hosts);
        services.AddSingleton<IHostExecutionCoordinator, HostExecutionCoordinator>();
        services.AddSingleton<IVisualStudioAttach>(debugger);
        services.AddSingleton(modules);
        modules.RegisterServices(services);
        await using var provider = services.BuildServiceProvider();
        var arguments = new List<string>
        {
            "run", typeof(ComposedRunCommandTests).Assembly.Location,
            "--host", "Revit", "--host-version", "2026", "--debug",
            "--test", "Sample.Fixture.PlainTest",
        };

        await ConsoleGate.WaitAsync(TestContext.Current.CancellationToken);
        var originalOut = Console.Out;
        using var stdout = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            var exitCode = await modules.RunAsync(arguments.ToArray(), provider);

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
        Assert.Equal(1234, debugger.DetachedProcessId);
        Assert.Contains("framework_id", stdout.ToString(), StringComparison.Ordinal);
    }

    private sealed class FakeHostSession(string pipeName) : IHostSession
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

    private sealed class FakeDebugger : IVisualStudioAttach
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

    private sealed record WireRequest(string Method, string Filter);

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
                RunRequest.TrySetResult(new WireRequest(request.Method, run.Selection.ProviderPayload ?? ""));
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
