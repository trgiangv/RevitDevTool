using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using DevTools.Ipc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Client.Tests.Harness;

/// <summary>In-process MCP host on a DevToolsMcp named pipe (live PID).</summary>
internal sealed class FakeMcpHostPipe : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly McpServerOptions _options;
    private readonly ServiceProvider _appServices;
    private readonly Task _bootstrapTask;
    private NamedPipeServerStream _serverPipe;
    private McpServer? _activeServer;
    private TaskCompletionSource _acceptingConnection = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private FakeMcpHostPipe(
        string pipeName,
        NamedPipeServerStream serverPipe,
        McpServerOptions options,
        ServiceProvider appServices)
    {
        PipeName = pipeName;
        _serverPipe = serverPipe;
        _options = options;
        _appServices = appServices;
        _bootstrapTask = BootstrapAsync(_cts.Token);
    }

    public string PipeName { get; }

    public int ListenGeneration { get; private set; }

    public static Task<FakeMcpHostPipe> StartAsync(
        string? version = null,
        IEnumerable<McpServerTool>? tools = null,
        IEnumerable<McpServerResource>? resources = null,
        CancellationToken cancellationToken = default)
    {
        version ??= Guid.NewGuid().ToString("N")[..8];
        var pipeName = HostPipeName.FormatMcp("Revit", version, Environment.ProcessId);
        var serverPipe = CreateServerPipe(pipeName);

        var toolCollection = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var tool in tools ?? DefaultTools())
            toolCollection.TryAdd(tool);

        var resourceCollection = new McpServerResourceCollection();
        foreach (var resource in resources ?? DefaultResources())
            resourceCollection.TryAdd(resource);

        var options = new McpServerOptions
        {
            ServerInfo = new Implementation { Name = "fake-host", Version = "1.0.0" },
            ToolCollection = toolCollection,
            ResourceCollection = resourceCollection,
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability { ListChanged = true },
                Resources = new ResourcesCapability { ListChanged = true }
            }
        };

        var appServices = new ServiceCollection()
            .AddSingleton(NullLoggerFactory.Instance)
            .BuildServiceProvider();
        return Task.FromResult(new FakeMcpHostPipe(pipeName, serverPipe, options, appServices));
    }

    public bool AddTool(McpServerTool tool) => _options.ToolCollection!.TryAdd(tool);

    public async Task EndCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_activeServer is null)
            return;

        await _activeServer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        _activeServer = null;
    }

    public async Task<McpClient> ConnectClientAsync(CancellationToken cancellationToken = default)
    {
        await _acceptingConnection.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        var clientPipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await clientPipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
        return await McpClient.CreateAsync(
            new StreamClientTransport(clientPipe, clientPipe, NullLoggerFactory.Instance),
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try { await _bootstrapTask.ConfigureAwait(false); } catch { /* ignored */ }

        await _serverPipe.DisposeAsync().ConfigureAwait(false);
        _cts.Dispose();
        _appServices.Dispose();
    }

    private async Task BootstrapAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var listening = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _acceptingConnection = listening;
            listening.SetResult();
            ListenGeneration++;

            try
            {
                await _serverPipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }

            var transport = new StreamServerTransport(_serverPipe, _serverPipe, "fake-host", NullLoggerFactory.Instance);
            var server = McpServer.Create(transport, _options, NullLoggerFactory.Instance, _appServices);
            _activeServer = server;
            try
            {
                await server.RunAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                /* client disconnected */
            }
            finally
            {
                if (ReferenceEquals(_activeServer, server))
                    _activeServer = null;

                await server.DisposeAsync().ConfigureAwait(false);
            }

            if (ct.IsCancellationRequested)
                return;

            if (_serverPipe.IsConnected)
                _serverPipe.Disconnect();

            await _serverPipe.DisposeAsync().ConfigureAwait(false);
            _serverPipe = CreateServerPipe(PipeName);
        }
    }

    private static IEnumerable<McpServerTool> DefaultTools() =>
    [
        McpServerTool.Create(
            (string message) => $"echo:{message}",
            new McpServerToolCreateOptions { Name = "echo", Description = "Echo" }),
        McpServerTool.Create(
            () =>
            {
                throw new InputRequiredException(requestState: "client-round1");
#pragma warning disable CS0162
                return "";
#pragma warning restore CS0162
            },
            new McpServerToolCreateOptions { Name = "needs_input", Description = "MRTR round 1" })
    ];

    private static IEnumerable<McpServerResource> DefaultResources() =>
    [
        McpServerResource.Create(
            () => new TextResourceContents { Uri = "revit://version", Text = "2025", MimeType = "text/plain" },
            new McpServerResourceCreateOptions { UriTemplate = "revit://version" }),
        McpServerResource.Create(
            (string id) => new TextResourceContents
            {
                Uri = $"revit://element/{id}",
                Text = $"element-{id}",
                MimeType = "text/plain"
            },
            new McpServerResourceCreateOptions { UriTemplate = "revit://element/{id}" })
    ];

    private static NamedPipeServerStream CreateServerPipe(string pipeName)
    {
        var security = new PipeSecurity();
        var currentUser = WindowsIdentity.GetCurrent();
        Assert.IsNotNull(currentUser.User);
        security.AddAccessRule(new PipeAccessRule(
            currentUser.User,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            5,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
    }
}
