using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Ipc;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Utils;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ZLogger;

namespace DevTools.Daemon.Mcp.Processes;

/// <summary>One SDK <see cref="McpClient"/> session over a DevToolsMcp named pipe.</summary>
public sealed class ProcessSession : IProcessSession
{
    internal static readonly TimeSpan PipeConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CatalogListenAckTimeout = TimeSpan.FromSeconds(10);

    private readonly ILogger _logger;
    private readonly List<IAsyncDisposable> _notificationRegs = [];
    private CancellationTokenSource? _listenCts;
    private Task? _listen;
    private int _disposed;

    internal SemaphoreSlim CatalogRefreshGate { get; } = new(1, 1);

    public event Action? CatalogChanged;

    public InstanceInfo Info { get; }
    public string PipeName { get; }
    public int ProcessId => Info.ProcessId;
    public bool IsConnected => !Client.Completion.IsCompleted;
    public McpClient Client { get; }

    private ProcessSession(string pipeName, InstanceInfo info, McpClient client, ILogger logger)
    {
        PipeName = pipeName;
        Info = info;
        Client = client;
        _logger = logger;
    }

    public static async Task<ProcessSession> ConnectAsync(
        string pipeName,
        ILoggerFactory loggerFactory,
        ILogger logger,
        CancellationToken ct)
    {
        if (!HostPipeName.TryParse(pipeName, out var host, out var version, out var pid))
            throw new InvalidOperationException($"Invalid MCP pipe name: {pipeName}");
        if (!McpPipeScanner.IsProcessAlive(pid))
            throw new IOException($"Host process {pid} is not running.");

        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(PipeConnectTimeout);
        try
        {
            await pipe.ConnectAsync(connectCts.Token).ConfigureAwait(false);
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(pipe, pipe, loggerFactory),
            loggerFactory: loggerFactory,
            cancellationToken: ct).ConfigureAwait(false);

        var session = new ProcessSession(
            pipeName,
            new InstanceInfo { HostApp = host, VersionNumber = version, ProcessId = pid },
            client,
            logger);
        try
        {
            session.RegisterCatalogNotifications();
            await session.StartCatalogListenAsync(ct).ConfigureAwait(false);
            logger.ZLogDebug($"MCP session ready for {pipeName}");
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private void RegisterCatalogNotifications()
    {
        _notificationRegs.Add(Client.RegisterNotificationHandler(
            NotificationMethods.ToolListChangedNotification,
            (_, _) =>
            {
                CatalogChanged?.Invoke();
                return default;
            }));
        _notificationRegs.Add(Client.RegisterNotificationHandler(
            NotificationMethods.ResourceListChangedNotification,
            (_, _) =>
            {
                CatalogChanged?.Invoke();
                return default;
            }));
    }

    /// <summary>
    /// Protocol 2026-07-28 delivers <c>list_changed</c> only on <c>subscriptions/listen</c>.
    /// Older initialize-handshake sessions still broadcast it on the pipe.
    /// </summary>
    private async Task StartCatalogListenAsync(CancellationToken ct)
    {
        if (!UsesPerRequestSubscriptions(Client))
            return;

        var acknowledged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _notificationRegs.Add(Client.RegisterNotificationHandler(
            NotificationMethods.SubscriptionsAcknowledgedNotification,
            (_, _) =>
            {
                acknowledged.TrySetResult();
                return default;
            }));

        _listenCts = new CancellationTokenSource();
        _listen = ListenForCatalogChangesAsync(_listenCts.Token);

        using var ackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        ackCts.CancelAfter(CatalogListenAckTimeout);
        await acknowledged.Task.WaitAsync(ackCts.Token).ConfigureAwait(false);
    }

    private async Task ListenForCatalogChangesAsync(CancellationToken ct)
    {
        try
        {
            await Client.SendRequestAsync<SubscriptionsListenRequestParams, EmptyResult>(
                RequestMethods.SubscriptionsListen,
                new SubscriptionsListenRequestParams
                {
                    Notifications = new SubscriptionsListenNotifications
                    {
                        ToolsListChanged = true,
                        ResourcesListChanged = true,
                    }
                },
                cancellationToken: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.ZLogWarning(ex, $"MCP catalog subscription ended for {PipeName}");
        }
    }

    private static bool UsesPerRequestSubscriptions(McpClient client)
    {
        var version = client.NegotiatedProtocolVersion;
        return !string.IsNullOrEmpty(version)
            && string.CompareOrdinal(version, McpSpecKeys.ProtocolVersions.Current) >= 0;
    }

    private async Task StopCatalogListenAsync()
    {
        if (_listenCts is null)
            return;

        await _listenCts.CancelAsync().ConfigureAwait(false);
        if (_listen is not null)
            await _listen.ConfigureAwait(false);

        _listenCts.Dispose();
        _listenCts = null;
        _listen = null;
    }

    public async Task<Result> CallToolPassthroughAsync(CallToolRequestParams parameters, CancellationToken ct = default)
    {
        var response = await McpClientPassthrough.SendAsync(Client, parameters, ct).ConfigureAwait(false);
        if (response.Result is JsonObject resultObj &&
            resultObj.TryGetPropertyValue(McpSpecKeys.ResultType.Key, out var resultTypeNode) &&
            resultTypeNode?.GetValue<string>() is McpSpecKeys.ResultType.InputRequired)
        {
            return (InputRequiredResult?)response.Result.Deserialize(ToolHelpers.ProtocolOptions.GetTypeInfo(typeof(InputRequiredResult)))
                ?? throw new JsonException("Failed to deserialize host InputRequiredResult.");
        }

        return (CallToolResult?)response.Result.Deserialize(ToolHelpers.ProtocolOptions.GetTypeInfo(typeof(CallToolResult)))
            ?? throw new JsonException("Failed to deserialize host CallToolResult.");
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
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        await StopCatalogListenAsync().ConfigureAwait(false);
        foreach (var registration in _notificationRegs)
        {
            try
            {
                await registration.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.ZLogWarning(ex, $"Failed to remove MCP notification handler for {PipeName}");
            }
        }

        _notificationRegs.Clear();
        await Client.DisposeAsync().ConfigureAwait(false);
    }
}
