using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Hosting;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Host.Tests;

public sealed class MarshaledTestingRequestHandlerTests
{
    [Fact]
    public async Task Run_is_marshaled_but_hello_is_not()
    {
        var executor = new TrackingExecutor();
        var provider = new RecordingProvider();
        var handler = new MarshaledTestingRequestHandler(
            new TestingProviderRegistry([provider]),
            new StubHostInfo(),
            executor);

        var hello = JsonSerializer.SerializeToElement(
            new TestingHelloRequest(TestingProtocol.CurrentVersion, provider.FrameworkId),
            TestingJsonContext.Default.TestingHelloRequest);
        var helloResponse = await handler.HandleAsync(
            "hello", TestingProtocol.Hello, hello, TestContext.Current.CancellationToken);
        Assert.False(helloResponse.IsError);
        Assert.Equal(0, executor.ExecutionCount);

        var runId = Guid.NewGuid();
        var run = JsonSerializer.SerializeToElement(
            new TestingRunRequest(
                TestingProtocol.CurrentVersion,
                runId,
                provider.FrameworkId,
                new TestingAssemblyReference(@"C:\tests\Sample.dll", "net10.0-windows", "hash"),
                new TestingSelection(["opaque-id"]),
                new Dictionary<string, string>()),
            TestingJsonContext.Default.TestingRunRequest);
        var runResponse = await handler.HandleAsync(
            "run", TestingProtocol.Run, run, TestContext.Current.CancellationToken);

        Assert.False(runResponse.IsError);
        Assert.Equal(1, executor.ExecutionCount);
        Assert.False(executor.LastToken.CanBeCanceled);
        Assert.Equal(runId, provider.LastRunId);
    }

    private sealed class TrackingExecutor : IHostContextExecutor
    {
        public int ExecutionCount { get; private set; }

        public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default)
        {
            ExecutionCount++;
            LastToken = token;
            return Task.FromResult(handler());
        }

        public CancellationToken LastToken { get; private set; }

        public Task ExecuteAsync(Action action, CancellationToken token = default)
        {
            ExecutionCount++;
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProvider : IHostTestFrameworkProvider
    {
        public string FrameworkId => "example";
        public Guid? LastRunId { get; private set; }

        public TestingRunResponse Run(TestingRunRequest request, ITestingEventSink eventSink,
            CancellationToken cancellationToken)
        {
            LastRunId = request.RunId;
            return new TestingRunResponse(request.RunId, FrameworkId, "generation", [],
                TestingCancellationState.None, null, null);
        }

        public bool Cancel(Guid runId) => false;
    }

    private sealed class StubHostInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber => "2025";
        public string? VersionBuild => null;
        public int ProcessId => Environment.ProcessId;
    }
}
