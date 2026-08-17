using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Hosting;
using DevTools.NUnit.Host;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host;
using DevTools.Testing.Transport;

namespace DevTools.NUnit.Host.Tests;

public sealed class MarshaledTestingRequestHandlerTests
{
    [Fact]
    public void Supported_methods_are_testing_only()
    {
        var handler = CreateHandler(new TrackingExecutor(), new RecordingProvider());

        Assert.Contains(TestingProtocol.Hello, handler.SupportedMethods, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(TestingProtocol.Run, handler.SupportedMethods, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(TestingProtocol.Cancel, handler.SupportedMethods, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("nunit/hello", handler.SupportedMethods, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("nunit/run", handler.SupportedMethods, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("nunit/cancel", handler.SupportedMethods, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("testing/discover", handler.SupportedMethods, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Hello_does_not_marshal_onto_the_host_thread()
    {
        var executor = new TrackingExecutor();
        var handler = CreateHandler(executor, new RecordingProvider());
        var hello = JsonSerializer.SerializeToElement(
            new TestingHelloRequest(TestingProtocol.CurrentVersion, NUnitFramework.Id),
            TestingJsonContext.Default.TestingHelloRequest);

        var response = await handler.HandleAsync(
            "hello-1",
            TestingProtocol.Hello,
            hello,
            TestContext.Current.CancellationToken);

        Assert.False(executor.ExecuteCalled);
        Assert.False(response.IsError);
        Assert.NotNull(response.Result);
    }

    [Fact]
    public async Task Run_marshals_onto_the_host_thread()
    {
        var executor = new TrackingExecutor();
        var provider = new RecordingProvider();
        var handler = CreateHandler(executor, provider);
        var runId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var request = JsonSerializer.SerializeToElement(
            new TestingRunRequest(
                TestingProtocol.CurrentVersion,
                runId,
                NUnitFramework.Id,
                new TestingAssemblyReference(@"C:\tests\Sample.dll", "net10.0-windows", "hash"),
                new TestingSelection(["opaque-id"]),
                new Dictionary<string, string>()),
            TestingJsonContext.Default.TestingRunRequest);

        var response = await handler.HandleAsync(
            "run-1",
            TestingProtocol.Run,
            request,
            TestContext.Current.CancellationToken);

        Assert.True(executor.ExecuteCalled);
        Assert.True(provider.RunCalled);
        Assert.Equal(runId, provider.LastRunId);
        Assert.False(response.IsError);
        Assert.NotNull(response.Result);
    }

    private static MarshaledTestingRequestHandler CreateHandler(
        IHostContextExecutor executor,
        IHostTestFrameworkProvider provider) =>
        new(
            new TestingProviderRegistry([provider]),
            new StubHostInfo(),
            executor);

    private sealed class TrackingExecutor : IHostContextExecutor
    {
        public bool ExecuteCalled { get; private set; }

        public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default)
        {
            ExecuteCalled = true;
            return Task.FromResult(handler());
        }

        public Task ExecuteAsync(Action action, CancellationToken token = default)
        {
            ExecuteCalled = true;
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProvider : IHostTestFrameworkProvider
    {
        public string FrameworkId => NUnitFramework.Id;
        public bool RunCalled { get; private set; }
        public Guid? LastRunId { get; private set; }

        public TestingRunResponse Run(
            TestingRunRequest request,
            ITestingEventSink eventSink,
            CancellationToken cancellationToken)
        {
            RunCalled = true;
            LastRunId = request.RunId;
            return new TestingRunResponse(
                request.RunId,
                FrameworkId,
                "gen-test",
                Array.Empty<TestingCaseResult>(),
                TestingCancellationState.None,
                null,
                null);
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
