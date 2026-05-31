using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using DevTools.McpParser.Models;

namespace DevTools.McpServer;

public sealed class HostBridgeClient : IAsyncDisposable
{
    private readonly BridgePipeConnection _connection;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<BridgeMessage>> _pending = new();
    private int _idCounter;
    private volatile bool _connected;

    public event Action? ToolsChanged;
    public event Action<InstanceInfo>? DocumentChanged;
    public event Action? Disconnected;

    public string PipeName { get; }
    public InstanceInfo? Info { get; private set; }
    public bool IsConnected => _connected;

    private HostBridgeClient(string pipeName, BridgePipeConnection connection)
    {
        PipeName = pipeName;
        _connection = connection;
        _connected = true;

        _connection.MessageReceived += OnMessageReceived;
        _connection.Disconnected += OnDisconnected;
        _connection.StartReadLoop();
    }

    public static async Task<HostBridgeClient> ConnectAsync(string pipeName, CancellationToken ct = default)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(ct).ConfigureAwait(false);

        var connection = new BridgePipeConnection(pipe);
        var client = new HostBridgeClient(pipeName, connection);

        var infoResponse = await client.RequestAsync(BridgeMethods.InstanceInfo, ct: ct).ConfigureAwait(false);
        if (infoResponse.Result is { } result)
            client.Info = JsonSerializer.Deserialize<InstanceInfo>(result.GetRawText(), BridgePipeConnection.JsonOptions);

        return client;
    }

    public async Task<BridgeMessage> RequestAsync(string method, JsonElement? @params = null, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _idCounter).ToString();
        var tcs = new TaskCompletionSource<BridgeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        try
        {
            var request = BridgeMessage.Request(id, method, @params);
            await _connection.WriteAsync(request, ct).ConfigureAwait(false);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(60));
            return await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private void OnMessageReceived(BridgeMessage msg)
    {
        if (msg is { Type: BridgeMessage.TypeResponse, Id: not null })
        {
            if (_pending.TryRemove(msg.Id, out var tcs))
                tcs.TrySetResult(msg);
            return;
        }

        if (msg.Type == BridgeMessage.TypeNotification)
            HandleNotification(msg);
    }

    private void HandleNotification(BridgeMessage msg)
    {
        switch (msg.Method)
        {
            case BridgeMethods.NotifyToolsChanged:
                ToolsChanged?.Invoke();
                break;
            case BridgeMethods.NotifyDocumentChanged:
                HandleDocumentChanged(msg.Params);
                break;
        }
    }

    private void HandleDocumentChanged(JsonElement? @params)
    {
        if (@params is not { } p) return;
        var info = JsonSerializer.Deserialize<InstanceInfo>(p.GetRawText(), BridgePipeConnection.JsonOptions);
        if (info is null) return;
        Info = info;
        DocumentChanged?.Invoke(info);
    }

    private void CancelPendingRequests()
    {
        foreach (var kvp in _pending)
        {
            if (_pending.TryRemove(kvp.Key, out var tcs))
                tcs.TrySetCanceled();
        }
    }

    private void OnDisconnected()
    {
        _connected = false;
        CancelPendingRequests();
        Disconnected?.Invoke();
    }

    public ValueTask DisposeAsync()
    {
        _connected = false;
        CancelPendingRequests();
        _connection.Dispose();
        return ValueTask.CompletedTask;
    }
}
