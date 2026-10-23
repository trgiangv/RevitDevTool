using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;

namespace DevTools.TestRunner.Services;

public sealed class TestPipeClient : IAsyncDisposable
{
    private static readonly TimeSpan CancelSendTimeout = TimeSpan.FromSeconds(2);

    private readonly BridgePipeConnection _connection;
    private readonly Stream? _pipe;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<BridgeMessage>> _pending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, IProgress<TestEvent>> _runProgress = new();
    private volatile bool _disconnected;

    private TestPipeClient(Stream? pipe, BridgePipeConnection connection)
    {
        _pipe = pipe;
        _connection = connection;
        _connection.MessageReceived += OnMessage;
        _connection.Disconnected += OnDisconnected;
    }

    public static async Task<TestPipeClient> ConnectAsync(string pipeName, TimeSpan timeout, CancellationToken ct = default)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(timeout);
        await pipe.ConnectAsync(connectCts.Token).ConfigureAwait(false);

        var connection = new BridgePipeConnection(pipe);
        var client = new TestPipeClient(pipe, connection);
        connection.StartReadLoop();
        return client;
    }

    internal static TestPipeClient ConnectForTesting(Stream stream)
    {
        var connection = new BridgePipeConnection(stream);
        var client = new TestPipeClient(pipe: null, connection);
        connection.StartReadLoop();
        return client;
    }

    public async Task<TestHelloResponse> HelloAsync(TestFrameworkId frameworkId, CancellationToken ct = default)
    {
        var requestId = CreateRequestId("hello");
        var response = await SendCoreAsync(
            requestId,
            BridgeMessage.Request(
                requestId,
                TestingProtocol.Hello,
                JsonSerializer.SerializeToElement(
                    new TestHelloRequest(TestingProtocol.CurrentVersion, frameworkId),
                    TestingJsonContext.Default.TestHelloRequest)),
            progress: null,
            runId: null,
            onWritten: null,
            ct).ConfigureAwait(false);

        var hello = response.Result!.Value.Deserialize(TestingJsonContext.Default.TestHelloResponse)
            ?? throw new InvalidOperationException("Empty testing hello response.");

        if (!TestingProtocol.IsCompatible(hello.ProtocolVersion))
            throw new InvalidOperationException(TestingProtocol.CreateUnsupportedMessage(hello.ProtocolVersion));

        return hello;
    }

    public Task<TestRunResponse> RunAsync(
        TestRunRequest request,
        IProgress<TestEvent>? progress,
        CancellationToken ct = default) =>
        SendRunAsync(request, progress, ct);

    public Task CancelAsync(Guid runId, CancellationToken ct = default) =>
        SendCancelAsync(runId, ct);

    private async Task<TestRunResponse> SendRunAsync(
        TestRunRequest request,
        IProgress<TestEvent>? progress,
        CancellationToken ct)
    {
        var requestId = CreateRequestId("run");
        var runRequestSent = false;

        try
        {
            var response = await SendCoreAsync(
                requestId,
                BridgeMessage.Request(
                    requestId,
                    TestingProtocol.Run,
                    JsonSerializer.SerializeToElement(request, TestingJsonContext.Default.TestRunRequest)),
                progress,
                request.RunId,
                () => runRequestSent = true,
                ct).ConfigureAwait(false);

            return response.Result!.Value.Deserialize(TestingJsonContext.Default.TestRunResponse)
                ?? throw new InvalidOperationException("Empty testing run response.");
        }
        catch (OperationCanceledException) when (runRequestSent)
        {
            await TrySendCancelBestEffortAsync(request.RunId).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<BridgeMessage> SendCoreAsync(
        string id,
        BridgeMessage request,
        IProgress<TestEvent>? progress,
        Guid? runId,
        Action? onWritten,
        CancellationToken ct)
    {
        var waiter = new TaskCompletionSource<BridgeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, waiter))
            throw new InvalidOperationException($"Duplicate test pipe request id '{id}'.");

        if (runId is { } progressRun && progress is not null)
            _runProgress[progressRun] = progress;

        try
        {
            ThrowIfDisconnected();
            await _connection.WriteAsync(request, ct).ConfigureAwait(false);
            onWritten?.Invoke();
            return await waiter.Task.WaitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(id, out _);
            if (runId is { } activeRun)
                _runProgress.TryRemove(activeRun, out _);
        }
    }

    private void OnMessage(BridgeMessage message)
    {
        if (TryReportNotification(message))
            return;

        if (message.Type != BridgeMessage.TypeResponse || message.Id is null)
            return;

        if (!_pending.TryRemove(message.Id, out var waiter))
            return;

        try
        {
            EnsureSuccess(message);
            waiter.TrySetResult(message);
        }
        catch (Exception ex)
        {
            waiter.TrySetException(ex);
        }
    }

    private bool TryReportNotification(BridgeMessage message)
    {
        if (message.Type != BridgeMessage.TypeNotification || message.Params is null)
            return false;

        if (!string.Equals(message.Method, TestingProtocol.Progress, StringComparison.Ordinal))
            return false;

        var testingEvent = message.Params.Value.Deserialize(TestingJsonContext.Default.TestEvent);
        if (testingEvent is null)
            return true;

        if (_runProgress.TryGetValue(testingEvent.RunId, out var progress))
            progress.Report(testingEvent);

        return true;
    }

    private async Task TrySendCancelBestEffortAsync(Guid runId)
    {
        try
        {
            await SendCancelAsync(runId, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Best effort only.
        }
    }

    private async Task SendCancelAsync(Guid runId, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(CancelSendTimeout);

        await _connection.WriteAsync(
            BridgeMessage.Request(
                CreateRequestId("cancel"),
                TestingProtocol.Cancel,
                JsonSerializer.SerializeToElement(
                    new TestCancelRequest(runId),
                    TestingJsonContext.Default.TestCancelRequest)),
            timeoutCts.Token).ConfigureAwait(false);
    }

    private void OnDisconnected()
    {
        _disconnected = true;
        foreach (var pending in _pending)
        {
            if (_pending.TryRemove(pending.Key, out var waiter))
                waiter.TrySetException(new IOException("Host pipe disconnected."));
        }
    }

    private void ThrowIfDisconnected()
    {
        if (_disconnected)
            throw new IOException("Host pipe disconnected.");
    }

    private static void EnsureSuccess(BridgeMessage response)
    {
        if (!response.IsError)
            return;

        if (response.ErrorDetail?.Code == TestingProtocol.IncompatibleCode)
            throw new InvalidOperationException(response.ErrorMessage ?? "Testing protocol incompatible.");

        throw new InvalidOperationException(response.ErrorMessage ?? "Testing request failed.");
    }

    private static string CreateRequestId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    public ValueTask DisposeAsync()
    {
        _connection.Disconnected -= OnDisconnected;
        _connection.MessageReceived -= OnMessage;
        OnDisconnected();
        _connection.Dispose();
        _pipe?.Dispose();
        return ValueTask.CompletedTask;
    }
}
