using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace DevTools.Mcp.Client.Tests.Harness;

/// <summary>In-process MCP host on a DevToolsMcp named pipe (live PID); serves many clients like the real host.</summary>
internal sealed class FakeMcpHostPipe : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly McpServerPrimitiveCollection<McpServerTool> _tools;
    private readonly McpServerResourceCollection _resources;
    private readonly ServiceProvider _appServices;
    private readonly ConcurrentDictionary<McpServer, Task> _sessions = new();
    private readonly Task _acceptTask;

    private FakeMcpHostPipe(
        string pipeName,
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpServerResourceCollection resources,
        ServiceProvider appServices)
    {
        PipeName = pipeName;
        _tools = tools;
        _resources = resources;
        _appServices = appServices;
        _acceptTask = AcceptLoopAsync(_cts.Token);
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

        var toolCollection = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var tool in tools ?? DefaultTools())
            toolCollection.TryAdd(tool);

        var resourceCollection = new McpServerResourceCollection();
        foreach (var resource in resources ?? DefaultResources())
            resourceCollection.TryAdd(resource);

        var appServices = new ServiceCollection()
            .AddSingleton(NullLoggerFactory.Instance)
            .BuildServiceProvider();
        return Task.FromResult(new FakeMcpHostPipe(pipeName, toolCollection, resourceCollection, appServices));
    }

    public bool AddTool(McpServerTool tool) => _tools.TryAdd(tool);

    public async Task EndSessionsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var server in _sessions.Keys.ToArray())
            await server.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try { await _acceptTask.ConfigureAwait(false); } catch { /* ignored */ }
        try { await Task.WhenAll(_sessions.Values).ConfigureAwait(false); } catch { /* ignored */ }

        _cts.Dispose();
        _appServices.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var pipe = CreateServerPipe(PipeName);
            try
            {
                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
                return;
            }

            var server = McpServer.Create(
                new StreamServerTransport(pipe, pipe, "fake-host", NullLoggerFactory.Instance),
                CreateOptions(),
                NullLoggerFactory.Instance,
                _appServices);
            _sessions[server] = RunSessionAsync(server, pipe, ct);
        }
    }

    private async Task RunSessionAsync(McpServer server, NamedPipeServerStream pipe, CancellationToken ct)
    {
        await Task.Yield();
        try
        {
            await server.RunAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            /* client disconnected or host stopped */
        }
        finally
        {
            _sessions.TryRemove(server, out _);
            await server.DisposeAsync().ConfigureAwait(false);
            await pipe.DisposeAsync().ConfigureAwait(false);
        }
    }

    private McpServerOptions CreateOptions() => new()
    {
        ServerInfo = new Implementation { Name = "fake-host", Version = "1.0.0" },
        ToolCollection = _tools,
        ResourceCollection = _resources,
        Capabilities = new ServerCapabilities
        {
            Tools = new ToolsCapability { ListChanged = true },
            Resources = new ResourcesCapability { ListChanged = true }
        }
    };

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
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
    }
}
