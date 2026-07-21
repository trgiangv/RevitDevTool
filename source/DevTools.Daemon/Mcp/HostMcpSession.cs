using System.IO.Pipes;
using DevTools.Ipc;
using DevTools.Mcp.Routing;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace DevTools.Daemon.Mcp;

public sealed class HostMcpSession : IHostMcpSession
{
    private readonly NamedPipeClientStream _pipe;
    private readonly McpClient _client;
    private readonly ILogger<HostMcpSession> _logger;
    private readonly List<IAsyncDisposable> _notificationHandlers = [];
    private int _disposed;

    private HostMcpSession(
        NamedPipeClientStream pipe,
        McpClient client,
        HostInstanceDescriptor instance,
        int generation,
        ILogger<HostMcpSession> logger)
    {
        _pipe = pipe;
        _client = client;
        _logger = logger;
        Instance = instance;
        Generation = generation;
    }

    public HostInstanceDescriptor Instance { get; }
    public int Generation { get; }
    public bool IsConnected => Volatile.Read(ref _disposed) == 0 && !_client.Completion.IsCompleted;
    public event Action? CatalogChanged;
    public event Action? Disconnected;

    public static async Task<HostMcpSession> ConnectAsync(
        string pipeName,
        int generation,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (!HostPipeName.TryParse(pipeName, out var hostApp, out var hostVersion, out var processId))
            throw new ArgumentException("Invalid MCP host pipe name.", nameof(pipeName));

        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        McpClient? client = null;
        try
        {
            await pipe.ConnectAsync(ct).ConfigureAwait(false);
            var transport = new StreamClientTransport(pipe, pipe, loggerFactory);
            client = await McpClient.CreateAsync(
                    transport,
                    new McpClientOptions
                    {
                        ClientInfo = new Implementation
                        {
                            Name = "DevTools.Daemon",
                            Version = typeof(HostMcpSession).Assembly.GetName().Version?.ToString() ?? "unknown"
                        }
                    },
                    loggerFactory,
                    ct)
                .ConfigureAwait(false);

            if (!string.Equals(client.ServerInfo.Name, hostApp, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(client.ServerInfo.Version, hostVersion, StringComparison.OrdinalIgnoreCase))
            {
                throw new HostIdentityException("host_identity_mismatch", pipeName);
            }

            ProtocolVersionValidation.RequireHostProtocolVersion(client.ServerCapabilities, pipeName);

            var session = new HostMcpSession(
                pipe,
                client,
                new HostInstanceDescriptor(processId, hostApp, hostVersion, pipeName),
                generation,
                loggerFactory.CreateLogger<HostMcpSession>());
            await session.RegisterCatalogNotificationsAsync().ConfigureAwait(false);
            session._logger.LogInformation(
                "Host MCP session connected. PipeName={PipeName} ProcessId={ProcessId} Generation={Generation}",
                session.Instance.PipeName,
                session.Instance.ProcessId,
                session.Generation);
            _ = session.ObserveDisconnectAsync();
            return session;
        }
        catch
        {
            if (client is not null)
                await client.DisposeAsync().ConfigureAwait(false);
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct) =>
        await _client.ListToolsAsync(cancellationToken: ct).ConfigureAwait(false);

    public async Task<IList<McpClientPrompt>> ListPromptsAsync(CancellationToken ct) =>
        await _client.ListPromptsAsync(cancellationToken: ct).ConfigureAwait(false);

    public async Task<IList<McpClientResource>> ListResourcesAsync(CancellationToken ct) =>
        await _client.ListResourcesAsync(cancellationToken: ct).ConfigureAwait(false);

    public async Task<IList<McpClientResourceTemplate>> ListResourceTemplatesAsync(CancellationToken ct) =>
        await _client.ListResourceTemplatesAsync(cancellationToken: ct).ConfigureAwait(false);

    public async Task<CallToolResult> CallToolAsync(
        string name,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken ct) =>
        await _client.CallToolAsync(name, arguments, cancellationToken: ct).ConfigureAwait(false);

    public async Task<GetPromptResult> GetPromptAsync(
        string name,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken ct) =>
        await _client.GetPromptAsync(name, arguments, cancellationToken: ct).ConfigureAwait(false);

    public async Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct) =>
        await _client.ReadResourceAsync(uri, cancellationToken: ct).ConfigureAwait(false);

    private Task RegisterCatalogNotificationsAsync()
    {
        _notificationHandlers.Add(_client.RegisterNotificationHandler(
            NotificationMethods.ToolListChangedNotification,
            OnCatalogChangedAsync));
        _notificationHandlers.Add(_client.RegisterNotificationHandler(
            NotificationMethods.PromptListChangedNotification,
            OnCatalogChangedAsync));
        _notificationHandlers.Add(_client.RegisterNotificationHandler(
            NotificationMethods.ResourceListChangedNotification,
            OnCatalogChangedAsync));
        return Task.CompletedTask;
    }

    private ValueTask OnCatalogChangedAsync(JsonRpcNotification notification, CancellationToken ct)
    {
        CatalogChanged?.Invoke();
        return ValueTask.CompletedTask;
    }

    private async Task ObserveDisconnectAsync()
    {
        try
        {
            await _client.Completion.ConfigureAwait(false);
        }
        finally
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                _logger.LogInformation(
                    "Host MCP session disconnected. PipeName={PipeName} ProcessId={ProcessId} Generation={Generation}",
                    Instance.PipeName,
                    Instance.ProcessId,
                    Generation);
            }
            if (Volatile.Read(ref _disposed) == 0)
                Disconnected?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _logger.LogInformation(
            "Host MCP session disconnected. PipeName={PipeName} ProcessId={ProcessId} Generation={Generation}",
            Instance.PipeName,
            Instance.ProcessId,
            Generation);

        foreach (var registration in _notificationHandlers)
            await registration.DisposeAsync().ConfigureAwait(false);
        _notificationHandlers.Clear();

        await _client.DisposeAsync().ConfigureAwait(false);
        await _pipe.DisposeAsync().ConfigureAwait(false);
    }
}
