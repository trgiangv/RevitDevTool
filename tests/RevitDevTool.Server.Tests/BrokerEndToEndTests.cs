using System.IO.Pipes;
using System.Text.Json;
using DevTools.Daemon.Mcp;
using DevTools.Daemon.Mcp.Tools;
using DevTools.Execution.External.Mcp.Hosting;
using DevTools.Mcp.Routing;
using DevTools.Mcp.Routing.Broker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RevitDevTool.Server.Tests;

public sealed class BrokerEndToEndTests
{
    [Fact]
    public async Task SearchThenInvoke_UsesTwoExternalCallsAndDoesNotRelistTheHost()
    {
        await using var fixture = await BrokerFixture.CreateAsync(TestContext.Current.CancellationToken);

        var search = await fixture.CallAsync("devtools_search", new Dictionary<string, object?> { ["query"] = "execute_csharp_code" });
        var listsAfterSearch = fixture.Session.ListCalls;
        var invoke = await fixture.CallAsync("devtools_invoke", new Dictionary<string, object?> { ["target"] = "tool:execute_csharp_code" });

        Assert.Equal(2, fixture.ExternalCallCount);
        Assert.Equal(["devtools_search", "devtools_invoke"], fixture.ExternalCallNames);
        Assert.Equal(1, fixture.Tool.InvocationCount);
        Assert.Equal(listsAfterSearch, fixture.Session.ListCalls);
        Assert.Equal("42", Assert.IsType<TextContentBlock>(Assert.Single(invoke.Content)).Text);
        Assert.NotEmpty(search.Content);
    }

    [Fact]
    public async Task KnownTargetInvoke_UsesOneExternalCallAndOneHostCall()
    {
        await using var fixture = await BrokerFixture.CreateAsync(TestContext.Current.CancellationToken);

        var result = await fixture.CallAsync("devtools_invoke", new Dictionary<string, object?> { ["target"] = "tool:execute_csharp_code" });

        Assert.Equal(1, fixture.ExternalCallCount);
        Assert.Equal(["devtools_invoke"], fixture.ExternalCallNames);
        Assert.Equal(1, fixture.Tool.InvocationCount);
        Assert.Equal("42", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
    }

    private sealed class BrokerFixture : IAsyncDisposable
    {
        private readonly CancellationTokenSource stop = new();
        private readonly Task daemonTask;
        private readonly McpClient client;
        private readonly NamedPipeClientStream clientPipe;
        private readonly HostMcpServerHostedService host;
        private readonly ServiceProvider services;

        private BrokerFixture(CountingSession session, CountingTool tool, McpClient client, NamedPipeClientStream clientPipe,
            HostMcpServerHostedService host, ServiceProvider services, Task daemonTask, CancellationTokenSource stop)
        {
            Session = session;
            Tool = tool;
            this.client = client;
            this.clientPipe = clientPipe;
            this.host = host;
            this.services = services;
            this.daemonTask = daemonTask;
            this.stop = stop;
        }

        public CountingSession Session { get; }
        public CountingTool Tool { get; }
        public int ExternalCallCount { get; private set; }
        public List<string> ExternalCallNames { get; } = [];

        public static async Task<BrokerFixture> CreateAsync(CancellationToken ct)
        {
            var tool = new CountingTool();
            var hostOptions = new HostMcpServerOptionsFactory(new HostInfo(), [tool], [], []);
            var services = new ServiceCollection().BuildServiceProvider();
            var host = new HostMcpServerHostedService(hostOptions, NullLoggerFactory.Instance, services);
            await host.StartAsync(ct);
            var realSession = await HostMcpSession.ConnectAsync(McpPipeName.Format(Environment.ProcessId), NullLoggerFactory.Instance, ct);
            var session = new CountingSession(realSession);
            var manager = new SingleSessionManager(session);
            var broker = new BrokerCatalogIndex();
            broker.ReplaceSnapshots([HostCatalogSnapshot.Create(session.Instance,
                await session.ListToolsAsync(ct), await session.ListPromptsAsync(ct), await session.ListResourcesAsync(ct), await session.ListResourceTemplatesAsync(ct))]);

            var brokerTools = new DevToolsBrokerTools(broker, manager);
            McpServerPrimitiveCollection<McpServerTool> daemonTools =
            [McpServerTool.Create(typeof(DevToolsBrokerTools).GetMethod(nameof(DevToolsBrokerTools.Search))!, brokerTools),
             McpServerTool.Create(typeof(DevToolsBrokerTools).GetMethod(nameof(DevToolsBrokerTools.InvokeAsync))!, brokerTools)];
            var pipeName = $"revitdevtool-broker-{Guid.NewGuid():N}";
            var stop = new CancellationTokenSource();
            var daemonTask = RunDaemonAsync(pipeName, ToolHelpers.ConfigureGatewayOptions(daemonTools, [], []), services, stop.Token);

            var clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await clientPipe.ConnectAsync(5000, ct);
            var client = await McpClient.CreateAsync(new StreamClientTransport(clientPipe, clientPipe, NullLoggerFactory.Instance),
                new McpClientOptions { ClientInfo = new Implementation { Name = "external-test", Version = "1.0" } }, cancellationToken: ct);
            return new BrokerFixture(session, tool, client, clientPipe, host, services, daemonTask, stop);
        }

        public async Task<CallToolResult> CallAsync(string name, IReadOnlyDictionary<string, object?> arguments)
        {
            ExternalCallCount++;
            ExternalCallNames.Add(name);
            return await client.CallToolAsync(name, arguments, cancellationToken: TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            stop.Cancel();
            await client.DisposeAsync();
            await clientPipe.DisposeAsync();
            try { await daemonTask.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception) when (stop.IsCancellationRequested) { }
            await Session.DisposeAsync();
            await host.StopAsync(CancellationToken.None);
            await host.DisposeAsync();
            await services.DisposeAsync();
            stop.Dispose();
        }

        private static async Task RunDaemonAsync(string pipeName, McpServerOptions options, IServiceProvider services, CancellationToken ct)
        {
            await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await pipe.WaitForConnectionAsync(ct);
            await using var transport = new StreamServerTransport(pipe, pipe, pipeName, NullLoggerFactory.Instance);
            await using var server = McpServer.Create(transport, options, NullLoggerFactory.Instance, services);
            await server.RunAsync(ct);
        }
    }

    private sealed class HostInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber => "2027";
        public string? VersionBuild => null;
        public int ProcessId => Environment.ProcessId;
    }

    private sealed class CountingTool : McpServerTool
    {
        public int InvocationCount { get; private set; }
        public override Tool ProtocolTool { get; } = new() { Name = "execute_csharp_code", InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }) };
        public override IReadOnlyList<object> Metadata => [];
        public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return ValueTask.FromResult(new CallToolResult { Content = [new TextContentBlock { Text = "42" }] });
        }
    }

    private sealed class SingleSessionManager(IHostMcpSession session) : IInstanceManager
    {
        public IReadOnlyCollection<IHostMcpSession> Sessions => [session];
        public event Action? SessionsChanged { add { } remove { } }
        public IHostMcpSession? GetSessionByProcessId(int processId) => session.Instance.ProcessId == processId ? session : null;
    }

    private sealed class CountingSession(IHostMcpSession inner) : IHostMcpSession
    {
        public int ListCalls { get; private set; }
        public HostInstanceDescriptor Instance => inner.Instance;
        public bool IsConnected => inner.IsConnected;
        public event Action? CatalogChanged { add => inner.CatalogChanged += value; remove => inner.CatalogChanged -= value; }
        public event Action? Disconnected { add => inner.Disconnected += value; remove => inner.Disconnected -= value; }
        public Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct) { ListCalls++; return inner.ListToolsAsync(ct); }
        public Task<IList<McpClientPrompt>> ListPromptsAsync(CancellationToken ct) { ListCalls++; return inner.ListPromptsAsync(ct); }
        public Task<IList<McpClientResource>> ListResourcesAsync(CancellationToken ct) { ListCalls++; return inner.ListResourcesAsync(ct); }
        public Task<IList<McpClientResourceTemplate>> ListResourceTemplatesAsync(CancellationToken ct) { ListCalls++; return inner.ListResourceTemplatesAsync(ct); }
        public Task<CallToolResult> CallToolAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct) => inner.CallToolAsync(name, arguments, ct);
        public Task<GetPromptResult> GetPromptAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct) => inner.GetPromptAsync(name, arguments, ct);
        public Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct) => inner.ReadResourceAsync(uri, ct);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
