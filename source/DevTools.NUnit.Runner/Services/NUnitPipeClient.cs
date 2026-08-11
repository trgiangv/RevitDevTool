using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using DevTools.NUnit.Core.Compatibility;
using DevTools.NUnit.Core;
using DevTools.NUnit.Core.Contracts;

namespace DevTools.NUnit.Runner.Services;

public sealed class NUnitPipeClient : IAsyncDisposable
{
    private readonly BridgePipeConnection _connection;
    private readonly NamedPipeClientStream _pipe;
    private readonly ConcurrentQueue<BridgeMessage> _inbox = new();
    private volatile bool _disconnected;

    private NUnitPipeClient(NamedPipeClientStream pipe, BridgePipeConnection connection)
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

    public async Task<NUnitHelloResponse> HelloAsync(CancellationToken ct = default)
    {
        var response = await SendRequestAsync(
            "hello",
            NUnitProtocol.Hello,
            JsonSerializer.SerializeToElement(
                new NUnitHelloRequest(NUnitProtocol.CurrentVersion),
                NUnitJsonContext.Default.NUnitHelloRequest),
            progress: null,
            debugReady: null,
            ct).ConfigureAwait(false);

        return response.Result!.Value.Deserialize(NUnitJsonContext.Default.NUnitHelloResponse)
            ?? throw new InvalidOperationException("Empty hello response.");
    }

    public async Task<NUnitDiscoverResponse> DiscoverAsync(string assemblyPath, string? filter, CancellationToken ct = default)
    {
        var response = await SendRequestAsync(
            "discover",
            NUnitProtocol.Discover,
            JsonSerializer.SerializeToElement(
                new NUnitDiscoverRequest(assemblyPath, filter),
                NUnitJsonContext.Default.NUnitDiscoverRequest),
            progress: null,
            debugReady: null,
            ct).ConfigureAwait(false);

        return response.Result!.Value.Deserialize(NUnitJsonContext.Default.NUnitDiscoverResponse)
            ?? throw new InvalidOperationException("Empty discover response.");
    }

    public Task<NUnitRunResponse> RunAsync(
        string assemblyPath,
        string? filter,
        bool waitForDebugger,
        IProgress<NUnitProgressEvent>? progress,
        IProgress<NUnitDebugReadyEvent>? debugReady = null,
        CancellationToken ct = default)
    {
        var runId = Guid.NewGuid();
        return SendRunAsync(runId, assemblyPath, filter, waitForDebugger, progress, debugReady, ct);
    }

    private async Task<NUnitRunResponse> SendRunAsync(
        Guid runId,
        string assemblyPath,
        string? filter,
        bool waitForDebugger,
        IProgress<NUnitProgressEvent>? progress,
        IProgress<NUnitDebugReadyEvent>? debugReady,
        CancellationToken ct)
    {
        var response = await SendRequestAsync(
            "run",
            NUnitProtocol.Run,
            JsonSerializer.SerializeToElement(
                new NUnitRunRequest(runId, assemblyPath, filter, waitForDebugger),
                NUnitJsonContext.Default.NUnitRunRequest),
            progress,
            debugReady,
            ct).ConfigureAwait(false);

        return response.Result!.Value.Deserialize(NUnitJsonContext.Default.NUnitRunResponse)
            ?? throw new InvalidOperationException("Empty run response.");
    }

    private async Task<BridgeMessage> SendRequestAsync(
        string id,
        string method,
        JsonElement parameters,
        IProgress<NUnitProgressEvent>? progress,
        IProgress<NUnitDebugReadyEvent>? debugReady,
        CancellationToken ct)
    {
        await _connection.WriteAsync(BridgeMessage.Request(id, method, parameters), ct).ConfigureAwait(false);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            ThrowIfDisconnected();

            if (!_inbox.TryDequeue(out var message))
            {
                await Task.Delay(NUnitHostTiming.HostRequestPollIntervalMilliseconds, ct).ConfigureAwait(false);
                continue;
            }

            if (TryConsumeInboxMessage(message, id, progress, debugReady, out var response))
                return response!;
        }
    }

    private static bool TryConsumeInboxMessage(
        BridgeMessage message,
        string id,
        IProgress<NUnitProgressEvent>? progress,
        IProgress<NUnitDebugReadyEvent>? debugReady,
        out BridgeMessage? response)
    {
        response = null;

        if (TryReportNotification(message, progress, debugReady))
            return false;

        if (!IsMatchingResponse(message, id))
            return false;

        EnsureSuccess(message);
        response = message;
        return true;
    }

    private static bool TryReportNotification(
        BridgeMessage message,
        IProgress<NUnitProgressEvent>? progress,
        IProgress<NUnitDebugReadyEvent>? debugReady)
    {
        if (message.Type != BridgeMessage.TypeNotification || message.Params is null)
            return false;

        if (message.Method == NUnitProtocol.Progress)
        {
            var progressEvent = message.Params.Value.Deserialize(NUnitJsonContext.Default.NUnitProgressEvent);
            if (progressEvent is not null)
                progress?.Report(progressEvent);
            return true;
        }

        if (message.Method == NUnitProtocol.DebugReady)
        {
            var debugEvent = message.Params.Value.Deserialize(NUnitJsonContext.Default.NUnitDebugReadyEvent);
            if (debugEvent is not null)
                debugReady?.Report(debugEvent);
            return true;
        }

        return false;
    }

    private static bool IsMatchingResponse(BridgeMessage message, string id) =>
        message.Type == BridgeMessage.TypeResponse && message.Id == id;

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

    public ValueTask DisposeAsync()
    {
        _connection.Disconnected -= OnDisconnected;
        _connection.MessageReceived -= Enqueue;
        _connection.Dispose();
        _pipe.Dispose();
        return ValueTask.CompletedTask;
    }
}
