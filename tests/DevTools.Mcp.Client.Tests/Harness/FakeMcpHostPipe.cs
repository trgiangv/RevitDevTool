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
    private readonly Task _bootstrapTask;
    private Task? _serverTask;
    private McpServer? _server;
    private readonly NamedPipeServerStream _serverPipe;
    private readonly ServiceProvider _appServices;

    private FakeMcpHostPipe(
        string pipeName,
        NamedPipeServerStream serverPipe,
        McpServerOptions options,
        ServiceProvider appServices)
    {
        PipeName = pipeName;
        _serverPipe = serverPipe;
        _appServices = appServices;
        _bootstrapTask = BootstrapAsync(serverPipe, options, _cts.Token);
    }

    public string PipeName { get; }

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

        var appServices = TestMcpAppServices.Create();
        return Task.FromResult(new FakeMcpHostPipe(pipeName, serverPipe, options, appServices));
    }

    public async Task<McpClient> ConnectClientAsync(CancellationToken cancellationToken = default)
    {
        await _bootstrapTask.WaitAsync(cancellationToken).ConfigureAwait(false);
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
        if (_serverTask is not null)
        {
            try { await _serverTask.ConfigureAwait(false); } catch { /* ignored */ }
        }

        if (_server is not null)
            await _server.DisposeAsync().ConfigureAwait(false);

        await _serverPipe.DisposeAsync().ConfigureAwait(false);
        _cts.Dispose();
        _appServices.Dispose();
    }

    private async Task BootstrapAsync(NamedPipeServerStream serverPipe, McpServerOptions options, CancellationToken ct)
    {
        await serverPipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
        var transport = new StreamServerTransport(serverPipe, serverPipe, "fake-host", NullLoggerFactory.Instance);
        _server = McpServer.Create(transport, options, NullLoggerFactory.Instance, _appServices);
        _serverTask = _server.RunAsync(ct);
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
        Assert.NotNull(currentUser.User);
        security.AddAccessRule(new PipeAccessRule(
            currentUser.User,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
    }
}
