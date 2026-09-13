using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Hosting;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host;
using DevTools.Testing.Transport;

namespace DevTools.Testing.Host.Tests;

[TestClass]
public sealed class MarshaledTestRequestHandlerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Run_is_marshaled_but_hello_is_not()
    {
        var executor = new TrackingExecutor();
        var provider = new RecordingProvider();
        var handler = new MarshaledTestRequestHandler(
            new TestingProviderRegistry([provider]),
            new StubHostInfo(),
            executor);

        var hello = JsonSerializer.SerializeToElement(
            new TestHelloRequest(TestingProtocol.CurrentVersion, provider.FrameworkId),
            TestingJsonContext.Default.TestHelloRequest);
        var helloResponse = await handler.HandleAsync(
            "hello", TestingProtocol.Hello, hello, TestContext.CancellationToken);
        Assert.IsFalse(helloResponse.IsError);
        Assert.AreEqual(0, executor.ExecutionCount);

        var runId = Guid.NewGuid();
        var run = JsonSerializer.SerializeToElement(
            new TestRunRequest(
                TestingProtocol.CurrentVersion,
                runId,
                provider.FrameworkId,
                new TestAssemblyReference(@"C:\tests\Sample.dll"),
                TestSelection.FromTestIds(["opaque-id"])),
            TestingJsonContext.Default.TestRunRequest);
        var runResponse = await handler.HandleAsync(
            "run", TestingProtocol.Run, run, TestContext.CancellationToken);

        Assert.IsFalse(runResponse.IsError);
        Assert.AreEqual(1, executor.ExecutionCount);
        Assert.IsFalse(executor.LastToken.CanBeCanceled);
        Assert.AreEqual(runId, provider.LastRunId);
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

    private sealed class RecordingProvider : ITestFrameworkProvider
    {
        public TestFrameworkId FrameworkId => TestFrameworkId.NUnit;
        public Guid? LastRunId { get; private set; }

        public TestRunResponse Run(TestRunRequest request, ITestEventSink eventSink,
            CancellationToken cancellationToken)
        {
            LastRunId = request.RunId;
            return new TestRunResponse(request.RunId, FrameworkId, "generation", [],
                TestCancellationState.None, null, null);
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
