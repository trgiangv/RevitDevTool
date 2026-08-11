using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Logging;
using DevTools.NUnit.Core.Compatibility;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Core.Results;
using DevTools.NUnit.Host;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.NUnit.Host.Tests;

public sealed class NUnitRequestHandlerTests
{
    private const string SpikeFixtureAssemblyName = "DevTools.NUnit.Host.Spike.Fixtures.dll";

    [Fact]
    public async Task Discover_marshals_through_host_context_executor()
    {
        var executor = new RecordingHostContextExecutor(marshalToWorkerThread: true);
        var handler = CreateHandler(executor, CreateHost());

        var response = await handler.HandleAsync(
            "discover-1",
            NUnitProtocol.Discover,
            JsonSerializer.SerializeToElement(
                new NUnitDiscoverRequest(GetSpikeFixtureAssemblyPath(), null),
                NUnitJsonContext.Default.NUnitDiscoverRequest),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, executor.ExecuteCount);
        Assert.NotEqual(executor.CallingThreadId, executor.HandlerThreadId);
        Assert.False(response.IsError);
        var discoverResponse = response.Result!.Value.Deserialize(NUnitJsonContext.Default.NUnitDiscoverResponse);
        Assert.Equal(3, discoverResponse!.Cases.Count);
    }

    [Fact]
    public async Task Run_marshals_through_host_context_executor_and_not_on_pipe_thread()
    {
        var executor = new RecordingHostContextExecutor(marshalToWorkerThread: true);
        var handler = CreateHandler(executor, CreateHost());
        var pipeThreadId = Environment.CurrentManagedThreadId;

        var response = await handler.HandleAsync(
            "run-1",
            NUnitProtocol.Run,
            JsonSerializer.SerializeToElement(
                new NUnitRunRequest(Guid.NewGuid(), GetSpikeFixtureAssemblyPath(), null, false),
                NUnitJsonContext.Default.NUnitRunRequest),
            TestContext.Current.CancellationToken);

        Assert.Equal(pipeThreadId, executor.CallingThreadId);
        Assert.Equal(1, executor.ExecuteCount);
        Assert.NotEqual(executor.CallingThreadId, executor.HandlerThreadId);
        Assert.False(response.IsError);
    }

    [Fact]
    public async Task Run_sets_execution_guard_to_suppress()
    {
        ExecutionGuardContext.Mode = ExecutionGuardMode.Passthrough;
        var executor = new RecordingHostContextExecutor(marshalToWorkerThread: false);
        var handler = CreateHandler(executor, CreateHost());

        _ = await handler.HandleAsync(
            "run-guard",
            NUnitProtocol.Run,
            JsonSerializer.SerializeToElement(
                new NUnitRunRequest(Guid.NewGuid(), GetSpikeFixtureAssemblyPath(), null, false),
                NUnitJsonContext.Default.NUnitRunRequest),
            TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionGuardMode.Suppress, executor.CapturedGuardMode);
        ExecutionGuardContext.Mode = ExecutionGuardMode.Passthrough;
    }

    [Fact]
    public async Task Hello_rejects_incompatible_protocol_version()
    {
        var handler = CreateHandler(
            new RecordingHostContextExecutor(marshalToWorkerThread: false),
            CreateHost());

        var response = await handler.HandleAsync(
            "hello-1",
            NUnitProtocol.Hello,
            JsonSerializer.SerializeToElement(
                new NUnitHelloRequest(99),
                NUnitJsonContext.Default.NUnitHelloRequest),
            TestContext.Current.CancellationToken);

        Assert.True(response.IsError);
        Assert.Equal(ProtocolCompatibility.IncompatibleCode, response.ErrorDetail!.Code);
    }

    [Fact]
    public async Task Discover_returns_assembly_load_failure_for_missing_path()
    {
        var handler = CreateHandler(
            new RecordingHostContextExecutor(marshalToWorkerThread: false),
            CreateHost());

        var response = await handler.HandleAsync(
            "discover-missing",
            NUnitProtocol.Discover,
            JsonSerializer.SerializeToElement(
                new NUnitDiscoverRequest(@"C:\missing\tests.dll", null),
                NUnitJsonContext.Default.NUnitDiscoverRequest),
            TestContext.Current.CancellationToken);

        Assert.True(response.IsError);
        Assert.Equal(NUnitErrorCodes.AssemblyLoadFailed, response.ErrorDetail!.Code);
        Assert.Contains("missing", response.ErrorDetail.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(response.ErrorDetail.Data);
    }

    [Fact]
    public async Task Discover_returns_loader_details_for_invalid_assembly()
    {
        var temp = Path.Combine(Path.GetTempPath(), "DevTools.NUnit.Host.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var fakeDll = Path.Combine(temp, "FakeTests.dll");
        File.WriteAllBytes(fakeDll, [0x4D, 0x5A]);

        try
        {
            var handler = CreateHandler(
                new RecordingHostContextExecutor(marshalToWorkerThread: false),
                CreateHost());

            var response = await handler.HandleAsync(
                "discover-driver",
                NUnitProtocol.Discover,
                JsonSerializer.SerializeToElement(
                    new NUnitDiscoverRequest(fakeDll, null),
                    NUnitJsonContext.Default.NUnitDiscoverRequest),
                TestContext.Current.CancellationToken);

            Assert.True(response.IsError);
            Assert.Equal(NUnitErrorCodes.AssemblyLoadFailed, response.ErrorDetail!.Code);
            Assert.False(string.IsNullOrWhiteSpace(response.ErrorDetail.Message));
            Assert.NotNull(response.ErrorDetail.Data);
        }
        finally
        {
            try
            {
                if (Directory.Exists(temp))
                    Directory.Delete(temp, recursive: true);
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    [Fact]
    public async Task Run_reports_pass_and_fail_results()
    {
        var handler = CreateHandler(
            new RecordingHostContextExecutor(marshalToWorkerThread: false),
            CreateHost());

        var response = await handler.HandleAsync(
            "run-results",
            NUnitProtocol.Run,
            JsonSerializer.SerializeToElement(
                new NUnitRunRequest(Guid.NewGuid(), GetSpikeFixtureAssemblyPath(), null, false),
                NUnitJsonContext.Default.NUnitRunRequest),
            TestContext.Current.CancellationToken);

        Assert.False(response.IsError);
        var runResponse = response.Result!.Value.Deserialize(NUnitJsonContext.Default.NUnitRunResponse);
        Assert.Equal(3, runResponse!.Cases.Count);
        Assert.Equal(2, runResponse.Summary.Passed);
        Assert.Equal(1, runResponse.Summary.Failed);

        var failCase = runResponse.Cases.Single(test => test.Name == "Spike_Fail");
        Assert.Equal(NUnitOutcomes.Failed, failCase.Outcome);
        Assert.Contains("spike intentional failure", failCase.Message, StringComparison.Ordinal);

        var outputCase = runResponse.Cases.Single(test => test.Name == "Spike_Output");
        Assert.Contains("spike-output-marker", outputCase.Output ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("spike-trace-marker", outputCase.Output ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("spike-debug-marker", outputCase.Output ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_publishes_progress_notifications()
    {
        var published = new List<NUnitProgressEvent>();
        var handler = CreateHandler(
            new RecordingHostContextExecutor(marshalToWorkerThread: false),
            CreateHost());
        handler.NotificationSender = (_, data) =>
        {
            if (data is null)
                return;

            var progress = data.Value.Deserialize(NUnitJsonContext.Default.NUnitProgressEvent);
            if (progress is not null)
                published.Add(progress);
        };

        _ = await handler.HandleAsync(
            "run-progress",
            NUnitProtocol.Run,
            JsonSerializer.SerializeToElement(
                new NUnitRunRequest(Guid.NewGuid(), GetSpikeFixtureAssemblyPath(), null, false),
                NUnitJsonContext.Default.NUnitRunRequest),
            TestContext.Current.CancellationToken);

        Assert.Equal(3, published.Count);
        Assert.All(published, progress => Assert.NotEqual(Guid.Empty, progress.RunId));
    }

    [Fact]
    public async Task Cancel_requests_stop_on_active_run()
    {
        var cancelHost = new CancellableNUnitHost();
        var handler = CreateHandler(new RecordingHostContextExecutor(marshalToWorkerThread: false), cancelHost);
        var runId = Guid.NewGuid();

        var runTask = handler.HandleAsync(
            "run-cancel",
            NUnitProtocol.Run,
            JsonSerializer.SerializeToElement(
                new NUnitRunRequest(runId, GetSpikeFixtureAssemblyPath(), null, false),
                NUnitJsonContext.Default.NUnitRunRequest),
            TestContext.Current.CancellationToken);

        await cancelHost.WaitForRunStartedAsync(TestContext.Current.CancellationToken);
        _ = await handler.HandleAsync(
            "cancel-1",
            NUnitProtocol.Cancel,
            JsonSerializer.SerializeToElement(
                new NUnitCancelRequest(runId),
                NUnitJsonContext.Default.NUnitCancelRequest),
            TestContext.Current.CancellationToken);

        var response = await runTask;
        Assert.True(cancelHost.CancelRequested);
        Assert.False(response.IsError);
    }

    private static NUnitHost CreateHost() =>
        new(
            new NUnitReflectionRunner(
                new NUnitAssemblyLoader(),
                NullLogger<NUnitReflectionRunner>.Instance),
            NullLogger<NUnitHost>.Instance);

    private static NUnitRequestHandler CreateHandler(IHostContextExecutor executor, INUnitHost nunitHost) =>
        new(executor, nunitHost, new FakeHostAppInfo());

    private static string GetSpikeFixtureAssemblyPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, SpikeFixtureAssemblyName);
        Assert.True(File.Exists(path), $"Spike fixture assembly not found at '{path}'.");
        return path;
    }

    private sealed class FakeHostAppInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber => "2025";
        public string? VersionBuild => null;
        public int ProcessId => Environment.ProcessId;
    }

    private sealed class RecordingHostContextExecutor(bool marshalToWorkerThread) : IHostContextExecutor
    {
        public int ExecuteCount { get; private set; }
        public int CallingThreadId { get; private set; }
        public int HandlerThreadId { get; private set; }
        public ExecutionGuardMode CapturedGuardMode { get; private set; }

        public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default)
        {
            ExecuteCount++;
            CallingThreadId = Environment.CurrentManagedThreadId;
            CapturedGuardMode = ExecutionGuardContext.Mode;

            if (!marshalToWorkerThread)
            {
                HandlerThreadId = Environment.CurrentManagedThreadId;
                return Task.FromResult(handler());
            }

            return Task.Run(() =>
            {
                HandlerThreadId = Environment.CurrentManagedThreadId;
                return handler();
            }, token);
        }

        public Task ExecuteAsync(Action action, CancellationToken token = default) =>
            ExecuteAsync(() =>
            {
                action();
                return true;
            }, token);
    }

    private sealed class CancellableNUnitHost : INUnitHost
    {
        private readonly NUnitHost _inner = CreateHost();
        private readonly TaskCompletionSource _runStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool CancelRequested { get; private set; }

        public NUnitDiscoverResponse Discover(NUnitDiscoverRequest request) =>
            _inner.Discover(request);

        public NUnitRunResponse Run(NUnitRunRequest request, Action<NUnitProgressEvent> publish)
        {
            _runStarted.TrySetResult();
            return _inner.Run(request, publish);
        }

        public void Cancel(Guid runId)
        {
            CancelRequested = true;
            _inner.Cancel(runId);
        }

        public Task WaitForRunStartedAsync(CancellationToken cancellationToken = default) =>
            _runStarted.Task.WaitAsync(cancellationToken);
    }
}
