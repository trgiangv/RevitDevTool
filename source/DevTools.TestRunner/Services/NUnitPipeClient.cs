using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using DevTools.NUnit.Transport;
using DevTools.NUnit.Core.Compatibility;
using DevTools.NUnit.Core;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Core.Results;

namespace DevTools.TestRunner.Services;

public sealed class NUnitPipeClient : IAsyncDisposable
{
    private const int MaxPendingDiscardedResponses = 32;
    private static readonly TimeSpan CancelSendTimeout = TimeSpan.FromSeconds(2);

    private readonly BridgePipeConnection _connection;
    private readonly Stream? _pipe;
    private readonly ConcurrentQueue<BridgeMessage> _inbox = new();
    private readonly ConcurrentDictionary<string, byte> _discardedResponseIds = new(StringComparer.Ordinal);
    private volatile bool _disconnected;

    private NUnitPipeClient(Stream? pipe, BridgePipeConnection connection)
    {
        _pipe = pipe;
        _connection = connection;
        _connection.MessageReceived += Enqueue;
        _connection.Disconnected += OnDisconnected;
    }

    public static async Task<NUnitPipeClient> ConnectAsync(string pipeName, TimeSpan timeout, CancellationToken ct = default)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(timeout);
        await pipe.ConnectAsync(connectCts.Token).ConfigureAwait(false);

        var connection = new BridgePipeConnection(pipe);
        var client = new NUnitPipeClient(pipe, connection);
        connection.StartReadLoop();
        return client;
    }

    internal static NUnitPipeClient ConnectForTesting(Stream stream)
    {
        var connection = new BridgePipeConnection(stream);
        var client = new NUnitPipeClient(pipe: null, connection);
        connection.StartReadLoop();
        return client;
    }

    public async Task<NUnitHelloResponse> HelloAsync(CancellationToken ct = default)
    {
        var response = await SendRequestAsync(
            CreateRequestId("hello"),
            NUnitProtocol.Hello,
            JsonSerializer.SerializeToElement(
                new NUnitHelloRequest(NUnitProtocol.CurrentVersion),
                NUnitJsonContext.Default.NUnitHelloRequest),
            progress: null,
            activeRunId: null,
            ct).ConfigureAwait(false);

        var hello = response.Result!.Value.Deserialize(NUnitJsonContext.Default.NUnitHelloResponse)
            ?? throw new InvalidOperationException("Empty hello response.");

        ValidateHelloProtocolVersion(hello);
        return hello;
    }

    public async Task<NUnitDiscoverResponse> DiscoverAsync(string assemblyPath, string? filter, CancellationToken ct = default)
    {
        var normalizedFilter = NUnitRunnerFilter.Normalize(filter);
        var response = await SendRequestAsync(
            CreateRequestId("discover"),
            NUnitProtocol.Discover,
            JsonSerializer.SerializeToElement(
                new NUnitDiscoverRequest(assemblyPath, normalizedFilter),
                NUnitJsonContext.Default.NUnitDiscoverRequest),
            progress: null,
            activeRunId: null,
            ct).ConfigureAwait(false);

        return response.Result!.Value.Deserialize(NUnitJsonContext.Default.NUnitDiscoverResponse)
            ?? throw new InvalidOperationException("Empty discover response.");
    }

    public Task<NUnitRunResponse> RunAsync(
        string assemblyPath,
        string? filter,
        IProgress<NUnitProgressEvent>? progress,
        CancellationToken ct = default) =>
        SendRunAsync(Guid.NewGuid(), assemblyPath, filter, progress, ct);

    public Task CancelAsync(Guid runId, CancellationToken ct = default) =>
        SendCancelAsync(runId, ct);

    private async Task<NUnitRunResponse> SendRunAsync(
        Guid runId,
        string assemblyPath,
        string? filter,
        IProgress<NUnitProgressEvent>? progress,
        CancellationToken ct)
    {
        var normalizedFilter = NUnitRunnerFilter.Normalize(filter);
        var terminalCaseIds = new HashSet<string>(StringComparer.Ordinal);
        var requestId = CreateRequestId("run");
        var runRequestSent = false;

        try
        {
            await _connection.WriteAsync(
                BridgeMessage.Request(
                    requestId,
                    NUnitProtocol.Run,
                    JsonSerializer.SerializeToElement(
                        new NUnitRunRequest(runId, assemblyPath, normalizedFilter),
                        NUnitJsonContext.Default.NUnitRunRequest)),
                ct).ConfigureAwait(false);
            runRequestSent = true;

            var response = await WaitForResponseAsync(
                requestId,
                progress,
                activeRunId: runId,
                ct,
                terminalCaseIds).ConfigureAwait(false);

            return response.Result!.Value.Deserialize(NUnitJsonContext.Default.NUnitRunResponse)
                ?? throw new InvalidOperationException("Empty run response.");
        }
        catch (OperationCanceledException) when (runRequestSent)
        {
            MarkResponseDiscarded(requestId);
            await TrySendCancelBestEffortAsync(runId).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<BridgeMessage> SendRequestAsync(
        string id,
        string method,
        JsonElement parameters,
        IProgress<NUnitProgressEvent>? progress,
        Guid? activeRunId,
        CancellationToken ct)
    {
        await _connection.WriteAsync(BridgeMessage.Request(id, method, parameters), ct).ConfigureAwait(false);
        return await WaitForResponseAsync(id, progress, activeRunId, ct).ConfigureAwait(false);
    }

    private async Task<BridgeMessage> WaitForResponseAsync(
        string requestId,
        IProgress<NUnitProgressEvent>? progress,
        Guid? activeRunId,
        CancellationToken ct,
        HashSet<string>? terminalCaseIds = null)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            ThrowIfDisconnected();

            if (!_inbox.TryDequeue(out var message))
            {
                await Task.Delay(NUnitHostTiming.HostRequestPollIntervalMilliseconds, ct).ConfigureAwait(false);
                continue;
            }

            if (TryConsumeDiscardedResponse(message))
                continue;

            if (TryConsumeInboxMessage(message, requestId, progress, activeRunId, terminalCaseIds, out var response))
                return response!;

            _inbox.Enqueue(message);
            await Task.Delay(NUnitHostTiming.HostRequestPollIntervalMilliseconds, ct).ConfigureAwait(false);
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
        DrainInboxForDiscardedResponses();
        TrimExcessDiscardedResponseIds();
    }

    private void DrainInboxForDiscardedResponses()
    {
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
    }

    private void TrimExcessDiscardedResponseIds()
    {
        while (_discardedResponseIds.Count > MaxPendingDiscardedResponses)
        {
            var removed = false;
            foreach (var requestId in _discardedResponseIds.Keys)
            {
                if (_discardedResponseIds.TryRemove(requestId, out _))
                {
                    removed = true;
                    break;
                }
            }

            if (!removed)
                break;
        }
    }

    internal int PendingDiscardedResponseCountForTesting => _discardedResponseIds.Count;

    internal int InboxDepthForTesting => _inbox.Count;

    private static bool TryConsumeInboxMessage(
        BridgeMessage message,
        string requestId,
        IProgress<NUnitProgressEvent>? progress,
        Guid? activeRunId,
        HashSet<string>? terminalCaseIds,
        out BridgeMessage? response)
    {
        response = null;

        if (TryReportNotification(message, progress, activeRunId, terminalCaseIds))
            return false;

        if (!IsMatchingResponse(message, requestId))
            return false;

        EnsureSuccess(message);
        response = message;
        return true;
    }

    private static bool TryReportNotification(
        BridgeMessage message,
        IProgress<NUnitProgressEvent>? progress,
        Guid? activeRunId,
        HashSet<string>? terminalCaseIds)
    {
        if (message.Type != BridgeMessage.TypeNotification || message.Params is null)
            return false;

        if (message.Method != NUnitProtocol.Progress)
            return false;

        var progressEvent = message.Params.Value.Deserialize(NUnitJsonContext.Default.NUnitProgressEvent);
        if (progressEvent is null)
            return true;

        if (activeRunId is null || progressEvent.RunId != activeRunId)
            return true;

        if (terminalCaseIds is not null
            && IsTerminalOutcome(progressEvent.Case.Outcome)
            && !terminalCaseIds.Add(progressEvent.Case.Id))
        {
            return true;
        }

        progress?.Report(progressEvent);
        return true;
    }

    private static bool IsTerminalOutcome(string outcome) =>
        outcome is NUnitOutcomes.Passed
            or NUnitOutcomes.Failed
            or NUnitOutcomes.Skipped
            or NUnitOutcomes.Inconclusive
            or NUnitOutcomes.Error
            or NUnitOutcomes.Cancelled;

    private static bool IsMatchingResponse(BridgeMessage message, string requestId) =>
        message.Type == BridgeMessage.TypeResponse && message.Id == requestId;

    private async Task TrySendCancelBestEffortAsync(Guid runId)
    {
        try
        {
            await SendCancelAsync(runId, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Best effort only; never mask the original OperationCanceledException.
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
                NUnitProtocol.Cancel,
                JsonSerializer.SerializeToElement(
                    new NUnitCancelRequest(runId),
                    NUnitJsonContext.Default.NUnitCancelRequest)),
            timeoutCts.Token).ConfigureAwait(false);
    }

    private static void ValidateHelloProtocolVersion(NUnitHelloResponse hello)
    {
        var compatibilityError = ProtocolCompatibility.Validate(hello.ProtocolVersion);
        if (compatibilityError is null)
            return;

        throw new InvalidOperationException(compatibilityError.Message);
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

        if (response.ErrorDetail?.Code == ProtocolCompatibility.IncompatibleCode)
            throw new InvalidOperationException(response.ErrorMessage ?? "NUnit protocol incompatible.");

        throw new InvalidOperationException(FormatBridgeError(response));
    }

    private static string FormatBridgeError(BridgeMessage response)
    {
        var message = response.ErrorMessage ?? "NUnit request failed.";
        var details = TryReadBridgeErrorDetails(response.ErrorDetail?.Data);
        return string.IsNullOrWhiteSpace(details) ? message : $"{message}{Environment.NewLine}{details}";
    }

    private static string? TryReadBridgeErrorDetails(JsonElement? data)
    {
        if (data is not { } element || element.ValueKind != JsonValueKind.Object)
            return null;

        if (!element.TryGetProperty("details", out var details) || details.ValueKind != JsonValueKind.String)
            return null;

        return details.GetString();
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
