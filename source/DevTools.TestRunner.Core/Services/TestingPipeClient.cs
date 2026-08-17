using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;

namespace DevTools.TestRunner.Core.Services;

public sealed class TestingPipeClient : IAsyncDisposable
{
    private const int MaxPendingDiscardedResponses = 32;
    private static readonly TimeSpan CancelSendTimeout = TimeSpan.FromSeconds(2);

    private readonly BridgePipeConnection _connection;
    private readonly Stream? _pipe;
    private readonly ConcurrentQueue<BridgeMessage> _inbox = new();
    private readonly ConcurrentDictionary<string, byte> _discardedResponseIds = new(StringComparer.Ordinal);
    private volatile bool _disconnected;

    private TestingPipeClient(Stream? pipe, BridgePipeConnection connection)
    {
        _pipe = pipe;
        _connection = connection;
        _connection.MessageReceived += Enqueue;
        _connection.Disconnected += OnDisconnected;
    }

    public static async Task<TestingPipeClient> ConnectAsync(string pipeName, TimeSpan timeout, CancellationToken ct = default)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(timeout);
        await pipe.ConnectAsync(connectCts.Token).ConfigureAwait(false);

        var connection = new BridgePipeConnection(pipe);
        var client = new TestingPipeClient(pipe, connection);
        connection.StartReadLoop();
        return client;
    }

    internal static TestingPipeClient ConnectForTesting(Stream stream)
    {
        var connection = new BridgePipeConnection(stream);
        var client = new TestingPipeClient(pipe: null, connection);
        connection.StartReadLoop();
        return client;
    }

    public async Task<TestingHelloResponse> HelloAsync(string frameworkId, CancellationToken ct = default)
    {
        var response = await SendRequestAsync(
            CreateRequestId("hello"),
            TestingProtocol.Hello,
            JsonSerializer.SerializeToElement(
                new TestingHelloRequest(TestingProtocol.CurrentVersion, frameworkId),
                TestingJsonContext.Default.TestingHelloRequest),
            progress: null,
            activeRunId: null,
            ct).ConfigureAwait(false);

        var hello = response.Result!.Value.Deserialize(TestingJsonContext.Default.TestingHelloResponse)
            ?? throw new InvalidOperationException("Empty testing hello response.");

        if (!TestingProtocol.IsCompatible(hello.ProtocolVersion))
            throw new InvalidOperationException(TestingProtocol.CreateUnsupportedMessage(hello.ProtocolVersion));

        return hello;
    }

    public Task<TestingRunResponse> RunAsync(
        TestingRunRequest request,
        IProgress<TestingCaseResult>? progress,
        CancellationToken ct = default) =>
        SendRunAsync(request, progress, ct);

    public Task CancelAsync(Guid runId, CancellationToken ct = default) =>
        SendCancelAsync(runId, ct);

    private async Task<TestingRunResponse> SendRunAsync(
        TestingRunRequest request,
        IProgress<TestingCaseResult>? progress,
        CancellationToken ct)
    {
        var requestId = CreateRequestId("run");
        var runRequestSent = false;

        try
        {
            await _connection.WriteAsync(
                BridgeMessage.Request(
                    requestId,
                    TestingProtocol.Run,
                    JsonSerializer.SerializeToElement(request, TestingJsonContext.Default.TestingRunRequest)),
                ct).ConfigureAwait(false);
            runRequestSent = true;

            var response = await WaitForResponseAsync(
                requestId,
                progress,
                request.RunId,
                ct).ConfigureAwait(false);

            return response.Result!.Value.Deserialize(TestingJsonContext.Default.TestingRunResponse)
                ?? throw new InvalidOperationException("Empty testing run response.");
        }
        catch (OperationCanceledException) when (runRequestSent)
        {
            MarkResponseDiscarded(requestId);
            await TrySendCancelBestEffortAsync(request.RunId).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<BridgeMessage> SendRequestAsync(
        string id,
        string method,
        JsonElement parameters,
        IProgress<TestingCaseResult>? progress,
        Guid? activeRunId,
        CancellationToken ct)
    {
        await _connection.WriteAsync(BridgeMessage.Request(id, method, parameters), ct).ConfigureAwait(false);
        return await WaitForResponseAsync(id, progress, activeRunId, ct).ConfigureAwait(false);
    }

    private async Task<BridgeMessage> WaitForResponseAsync(
        string requestId,
        IProgress<TestingCaseResult>? progress,
        Guid? activeRunId,
        CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            ThrowIfDisconnected();

            if (!_inbox.TryDequeue(out var message))
            {
                await Task.Delay(RunnerTiming.PipeRequestPollIntervalMilliseconds, ct).ConfigureAwait(false);
                continue;
            }

            if (TryConsumeDiscardedResponse(message))
                continue;

            if (TryConsumeInboxMessage(message, requestId, progress, activeRunId, out var response))
                return response!;

            _inbox.Enqueue(message);
            await Task.Delay(RunnerTiming.PipeRequestPollIntervalMilliseconds, ct).ConfigureAwait(false);
        }
    }

    private bool TryConsumeDiscardedResponse(BridgeMessage message)
    {
        if (message.Type != BridgeMessage.TypeResponse || message.Id is null)
            return false;

        return _discardedResponseIds.TryRemove(message.Id, out _);
    }

    private void MarkResponseDiscarded(string requestId)
    {
        _discardedResponseIds.TryAdd(requestId, 0);
        if (_inbox.IsEmpty)
            return;

        var pending = new List<BridgeMessage>();
        while (_inbox.TryDequeue(out var message))
            pending.Add(message);

        foreach (var message in pending)
        {
            if (!TryConsumeDiscardedResponse(message))
                _inbox.Enqueue(message);
        }

        while (_discardedResponseIds.Count > MaxPendingDiscardedResponses)
        {
            foreach (var id in _discardedResponseIds.Keys)
            {
                if (_discardedResponseIds.TryRemove(id, out _))
                    break;
            }
        }
    }

    private static bool TryConsumeInboxMessage(
        BridgeMessage message,
        string requestId,
        IProgress<TestingCaseResult>? progress,
        Guid? activeRunId,
        out BridgeMessage? response)
    {
        response = null;

        if (TryReportNotification(message, progress, activeRunId))
            return false;

        if (message.Type != BridgeMessage.TypeResponse || message.Id != requestId)
            return false;

        EnsureSuccess(message);
        response = message;
        return true;
    }

    private static bool TryReportNotification(
        BridgeMessage message,
        IProgress<TestingCaseResult>? progress,
        Guid? activeRunId)
    {
        if (message.Type != BridgeMessage.TypeNotification || message.Params is null)
            return false;

        if (!string.Equals(message.Method, TestingProtocol.Progress, StringComparison.Ordinal))
            return false;

        var testingEvent = message.Params.Value.Deserialize(TestingJsonContext.Default.TestingEvent);
        if (testingEvent?.Case is null)
            return true;

        if (activeRunId is null || testingEvent.RunId != activeRunId)
            return true;

        progress?.Report(testingEvent.Case);
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
        var cancelRequestId = CreateRequestId("cancel");
        _discardedResponseIds.TryAdd(cancelRequestId, 0);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(CancelSendTimeout);

        await _connection.WriteAsync(
            BridgeMessage.Request(
                cancelRequestId,
                TestingProtocol.Cancel,
                JsonSerializer.SerializeToElement(
                    new TestingCancelRequest(runId),
                    TestingJsonContext.Default.TestingCancelRequest)),
            timeoutCts.Token).ConfigureAwait(false);
    }

    private void Enqueue(BridgeMessage message) => _inbox.Enqueue(message);

    private void OnDisconnected() => _disconnected = true;

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
        _connection.MessageReceived -= Enqueue;
        _connection.Dispose();
        _pipe?.Dispose();
        return ValueTask.CompletedTask;
    }
}
