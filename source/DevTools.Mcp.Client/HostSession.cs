using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Ipc;
using DevTools.Mcp.Core;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Sessions;
using DevTools.Mcp.Core.Utils;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ZLogger;

namespace DevTools.Mcp.Client;

/// <summary>One SDK <see cref="McpClient"/> session over a DevToolsMcp named pipe.</summary>
internal sealed class HostSession : IHostSession
{
    private readonly List<IAsyncDisposable> _notificationRegs = [];
    private volatile bool _connected = true;

    // Serializes catalog snapshots for this host pipe; list-changed notifications can coalesce safely.
    internal SemaphoreSlim CatalogRefreshGate { get; } = new(1, 1);

    public event Action? Disconnected;
    public event Action? CatalogChanged;

    public InstanceInfo Info { get; }
    public string PipeName { get; }
    public HostKey Key { get; }
    public bool IsConnected => _connected;
    public McpClient Client { get; }

    private HostSession(string pipeName, HostKey key, InstanceInfo info, McpClient client)
    {
        PipeName = pipeName;
        Key = key;
        Info = info;
        Client = client;
    }

    public static async Task<HostSession> ConnectAsync(
        string pipeName,
        string machineId,
        ILoggerFactory loggerFactory,
        ILogger logger,
        CancellationToken ct)
    {
        if (!HostPipeName.TryParse(pipeName, out var host, out var version, out var pid))
            throw new InvalidOperationException($"Invalid MCP pipe name: {pipeName}");

        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(ct).ConfigureAwait(false);

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(pipe, pipe, loggerFactory),
            loggerFactory: loggerFactory,
            cancellationToken: ct).ConfigureAwait(false);

        var info = new InstanceInfo
        {
            HostApp = host,
            VersionNumber = version,
            ProcessId = pid
        };
        var key = new HostKey(machineId, pid);
        var session = new HostSession(pipeName, key, info, client);

        session._notificationRegs.Add(client.RegisterNotificationHandler(
            NotificationMethods.ToolListChangedNotification,
            (_, _) =>
            {
                session.CatalogChanged?.Invoke();
                return default;
            }));
        session._notificationRegs.Add(client.RegisterNotificationHandler(
            NotificationMethods.ResourceListChangedNotification,
            (_, _) =>
            {
                session.CatalogChanged?.Invoke();
                return default;
            }));

        _ = client.Completion.ContinueWith(
            _ =>
            {
                session._connected = false;
                session.Disconnected?.Invoke();
            },
            TaskScheduler.Default);

        logger.ZLogDebug($"MCP session ready for {pipeName}");
        return session;
    }

    public async Task<HostToolCallOutcome> CallToolPassthroughAsync(CallToolRequestParams parameters, CancellationToken ct = default)
    {
        var response = await McpClientPassthrough.SendAsync(Client, parameters, ct).ConfigureAwait(false);
        if (response.Result is JsonObject resultObj &&
            resultObj.TryGetPropertyValue(McpSpecKeys.ResultType.Key, out var resultTypeNode) &&
            resultTypeNode?.GetValue<string>() is McpSpecKeys.ResultType.InputRequired)
        {
            var inputRequired = (InputRequiredResult?)response.Result.Deserialize(ToolHelpers.ProtocolOptions.GetTypeInfo(typeof(InputRequiredResult)))
                ?? throw new JsonException("Failed to deserialize host InputRequiredResult.");
            return HostToolCallOutcome.FromInputRequired(inputRequired);
        }

        var toolResult = (CallToolResult?)response.Result.Deserialize(ToolHelpers.ProtocolOptions.GetTypeInfo(typeof(CallToolResult)))
            ?? throw new JsonException("Failed to deserialize host CallToolResult.");
        return HostToolCallOutcome.FromToolResult(toolResult);
    }

    public Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct = default) =>
        Client.ReadResourceAsync(uri, cancellationToken: ct).AsTask();

    public Task<ReadResourceResult> ReadResourceAsync(
        string uriTemplate,
        IDictionary<string, JsonElement> arguments,
        CancellationToken ct = default)
    {
        IReadOnlyDictionary<string, object?> boxed =
            arguments.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
        return Client.ReadResourceAsync(uriTemplate, boxed, cancellationToken: ct).AsTask();
    }

    public async ValueTask DisposeAsync()
    {
        _connected = false;
        foreach (var registration in _notificationRegs)
        {
            try { await registration.DisposeAsync().ConfigureAwait(false); }
            catch { /* ignored */ }
        }

        _notificationRegs.Clear();
        await Client.DisposeAsync().ConfigureAwait(false);
    }
}
