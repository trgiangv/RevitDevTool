using System.IO.Pipes;
using System.Text.Json;
using DevTools.Daemon.Mcp;
using DevTools.Execution.External.Mcp.Hosting;
using DevTools.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RevitDevTool.Server.Tests;

public sealed class McpPipeNameTests
{
    [Fact]
    public void FormatAndParse_UseProtocolVersionAndPidOnly()
    {
        var name = McpPipeName.Format(4217);

        Assert.Equal("DevTools.Mcp.v2.4217", name);
        Assert.True(McpPipeName.TryParse(name, out var processId));
        Assert.Equal(4217, processId);
        Assert.False(McpPipeName.TryParse("DevTools_Mcp_Revit_2025_4217", out _));
    }
}

public sealed class McpNamedPipeIntegrationTests
{
    [Fact]
    public async Task InstanceManager_RetriesAdvertisedPipeAfterInitialConnectionFailure()
    {
        var pipeName = McpPipeName.Format(4217);
        var attempts = 0;
        await using var manager = CreateHostSessionManager(
            pipeName,
            (_, _) =>
            {
                attempts++;
                if (attempts == 1)
                    throw new IOException("Simulated initial connection failure.");

                return Task.FromResult<IHostMcpSession>(new TestMcpSession(pipeName));
            });
        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);
        Assert.Empty(manager.Sessions);

        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, attempts);
        Assert.Single(manager.Sessions);
    }

    [Fact]
    public async Task InstanceManager_ReconnectsAdvertisedPipeAfterSessionDisconnects()
    {
        var pipeName = McpPipeName.Format(4218);
        var attempts = 0;
        var firstSession = new TestMcpSession(pipeName);
        await using var manager = CreateHostSessionManager(
            pipeName,
            (_, _) => Task.FromResult<IHostMcpSession>(++attempts == 1 ? firstSession : new TestMcpSession(pipeName)));
        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);
        Assert.Same(firstSession, Assert.Single(manager.Sessions));

        var disconnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.SessionsChanged += () =>
        {
            if (manager.Sessions.Count == 0)
                disconnected.TrySetResult(true);
        };
        firstSession.Disconnect();
        await disconnected.Task.WaitAsync(TestContext.Current.CancellationToken);

        await manager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, attempts);
        Assert.Single(manager.Sessions);
    }

    [Fact]
    public async Task HostMcpSession_ListsCallsAndRaisesCatalogChanged()
    {
        var tools = new McpServerPrimitiveCollection<McpServerTool>
        {
            new TestTool("session_test")
        };
        var optionsFactory = new HostMcpServerOptionsFactory(new TestHostAppInfo(), tools, [], []);
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        await using var hostedService = new HostMcpServerHostedService(
            optionsFactory,
            NullLoggerFactory.Instance,
            serviceProvider);

        await hostedService.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await using var session = await HostMcpSession.ConnectAsync(
                McpPipeName.Format(Environment.ProcessId),
                NullLoggerFactory.Instance,
                TestContext.Current.CancellationToken);

            var listedTools = await session.ListToolsAsync(TestContext.Current.CancellationToken);
            Assert.Contains(listedTools, tool => tool.ProtocolTool.Name == "session_test");

            var result = await session.CallToolAsync(
                "session_test",
                null,
                TestContext.Current.CancellationToken);
            Assert.Equal("typed session", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);

            var catalogChanged = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            session.CatalogChanged += () => catalogChanged.TrySetResult(true);
            tools.Add(new TestTool("session_test_added"));

            await catalogChanged.Task.WaitAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            await hostedService.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task HostedService_StopsCleanlyWhenShutdownFollowsRecoverableAcceptFailure()
    {
        var acceptFailureObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var optionsFactory = new HostMcpServerOptionsFactory(new TestHostAppInfo(), [], [], []);
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        await using var hostedService = new HostMcpServerHostedService(
            optionsFactory,
            NullLoggerFactory.Instance,
            serviceProvider,
            _ =>
            {
                acceptFailureObserved.TrySetResult(true);
                throw new IOException("Simulated recoverable pipe creation failure.");
            });

        await hostedService.StartAsync(CancellationToken.None);
        await acceptFailureObserved.Task.WaitAsync(TestContext.Current.CancellationToken);

        await hostedService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HostedService_InitializesSdkClientWithHostMetadata()
    {
        var hostInfo = new TestHostAppInfo();
        var optionsFactory = new HostMcpServerOptionsFactory(
            hostInfo,
            [],
            [],
            []);
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        await using var hostedService = new HostMcpServerHostedService(
            optionsFactory,
            NullLoggerFactory.Instance,
            serviceProvider);

        await hostedService.StartAsync(CancellationToken.None);
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                McpPipeName.Format(Environment.ProcessId),
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await pipe.ConnectAsync(5000, TestContext.Current.CancellationToken);

            var transport = new StreamClientTransport(pipe, pipe, NullLoggerFactory.Instance);
            await using var client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions
                {
                    ClientInfo = new Implementation { Name = "integration-test", Version = "1.0" }
                },
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("Revit", client.ServerInfo.Name);
            Assert.Equal("2027", client.ServerInfo.Version);
        }
        finally
        {
            await hostedService.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task HostMcpSession_CancellationShutdownAndReconnect_DisposeSdkPipeSessions()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HostMcpSession.ConnectAsync(McpPipeName.Format(Environment.ProcessId + 10_000), NullLoggerFactory.Instance, cancelled.Token));

        var optionsFactory = new HostMcpServerOptionsFactory(new TestHostAppInfo(), [], [], []);
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        await using var firstHost = new HostMcpServerHostedService(optionsFactory, NullLoggerFactory.Instance, serviceProvider);
        await firstHost.StartAsync(TestContext.Current.CancellationToken);
        var disconnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (var firstSession = await HostMcpSession.ConnectAsync(McpPipeName.Format(Environment.ProcessId), NullLoggerFactory.Instance, TestContext.Current.CancellationToken))
        {
            firstSession.Disconnected += () => disconnected.TrySetResult(true);
            await firstHost.StopAsync(TestContext.Current.CancellationToken);
            await disconnected.Task.WaitAsync(TestContext.Current.CancellationToken);
        }

        await using var secondHost = new HostMcpServerHostedService(optionsFactory, NullLoggerFactory.Instance, serviceProvider);
        await secondHost.StartAsync(TestContext.Current.CancellationToken);
        await using var reconnected = await HostMcpSession.ConnectAsync(McpPipeName.Format(Environment.ProcessId), NullLoggerFactory.Instance, TestContext.Current.CancellationToken);
        Assert.True(reconnected.IsConnected);
        await secondHost.StopAsync(TestContext.Current.CancellationToken);
    }

    private sealed class TestHostAppInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber => "2027";
        public string? VersionBuild => null;
        public int ProcessId => Environment.ProcessId;
    }

    private sealed class TestTool(string name) : McpServerTool
    {
        public override Tool ProtocolTool { get; } = new()
        {
            Name = name,
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object" })
        };

        public override IReadOnlyList<object> Metadata => [];

        public override ValueTask<CallToolResult> InvokeAsync(
            RequestContext<CallToolRequestParams> request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<CallToolResult>(new()
            {
                Content = [new TextContentBlock { Text = "typed session" }]
            });
    }

    private static HostSessionManager CreateHostSessionManager(
        string pipeName,
        Func<string, CancellationToken, Task<IHostMcpSession>> connectAsync) =>
        new(
            NullLogger<HostSessionManager>.Instance,
            NullLoggerFactory.Instance,
            _ => new HashSet<string>([pipeName], StringComparer.OrdinalIgnoreCase),
            new DelegateHostSessionConnector(connectAsync),
            new ImmediateRetryClock());

    private sealed class DelegateHostSessionConnector(
        Func<string, CancellationToken, Task<IHostMcpSession>> connectAsync) : IHostSessionConnector
    {
        public Task<IHostMcpSession> ConnectAsync(string pipeName, CancellationToken ct) => connectAsync(pipeName, ct);
    }

    private sealed class ImmediateRetryClock : IRetryClock
    {
        private int _reads;
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch.AddSeconds(Interlocked.Increment(ref _reads));
        public Task DelayAsync(TimeSpan delay, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class TestMcpSession(string pipeName) : IHostMcpSession
    {
        public HostInstanceDescriptor Instance { get; } = new(4217, "Test", "1.0", pipeName);
        public bool IsConnected { get; private set; } = true;
        public event Action? CatalogChanged
        {
            add { }
            remove { }
        }
        public event Action? Disconnected;

        public Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct) =>
            Task.FromResult<IList<McpClientTool>>([]);

        public Task<IList<McpClientPrompt>> ListPromptsAsync(CancellationToken ct) =>
            Task.FromResult<IList<McpClientPrompt>>([]);

        public Task<IList<McpClientResource>> ListResourcesAsync(CancellationToken ct) =>
            Task.FromResult<IList<McpClientResource>>([]);

        public Task<IList<McpClientResourceTemplate>> ListResourceTemplatesAsync(CancellationToken ct) =>
            Task.FromResult<IList<McpClientResourceTemplate>>([]);

        public Task<CallToolResult> CallToolAsync(
            string name,
            IReadOnlyDictionary<string, object?>? arguments,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<GetPromptResult> GetPromptAsync(
            string name,
            IReadOnlyDictionary<string, object?>? arguments,
            CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct) =>
            throw new NotSupportedException();

        public void Disconnect()
        {
            IsConnected = false;
            Disconnected?.Invoke();
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }
}
