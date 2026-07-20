using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Daemon.Mcp;
using DevTools.Execution;
using DevTools.Execution.External;
using DevTools.Execution.External.Handlers;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.External.Mcp.Hosting;
using DevTools.Execution.External.Testing;
using DevTools.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RevitDevTool.Server.Tests;

public sealed class HostPipeNameTests
{
    [Theory]
    [InlineData("DevTools_Revit_2025_4217", "Revit", "2025", 4217)]
    [InlineData("DevTools_Rhino_8.0_99", "Rhino", "8.0", 99)]
    public void FormatAndParse_UseCanonicalHostIdentity(string name, string host, string version, int pid)
    {
        Assert.Equal(name, HostPipeName.Format(host, version, pid));
        Assert.True(HostPipeName.TryParse(name, out var actualHost, out var actualVersion, out var actualPid));
        Assert.Equal((host, version, pid), (actualHost, actualVersion, actualPid));
    }

    [Theory]
    [InlineData("DevTools.Mcp.v2.4217")]
    [InlineData("DevTools__2025_4217")]
    [InlineData("DevTools_Revit__4217")]
    [InlineData("DevTools_Revit_2025_0")]
    [InlineData("DevTools_Revit_2025_-1")]
    [InlineData("DevTools_Revit_2025_4217_extra")]
    [InlineData("DevTools_Revit_LT_2025_4217")]
    public void TryParse_RejectsNonCanonicalNames(string name) =>
        Assert.False(HostPipeName.TryParse(name, out _, out _, out _));

    [Theory]
    [InlineData(null, "2025", 4217)]
    [InlineData("", "2025", 4217)]
    [InlineData(" ", "2025", 4217)]
    [InlineData("Revit_LT", "2025", 4217)]
    [InlineData("Revit", null, 4217)]
    [InlineData("Revit", "", 4217)]
    [InlineData("Revit", " ", 4217)]
    [InlineData("Revit", "20_25", 4217)]
    public void Format_RejectsInvalidIdentitySegments(string? host, string? version, int pid) =>
        Assert.Throws<ArgumentException>(() => HostPipeName.Format(host!, version!, pid));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Format_RejectsNonPositiveProcessId(int pid) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => HostPipeName.Format("Revit", "2025", pid));
}

public sealed class ExecutionServiceRegistrationTests
{
    [Fact]
    public void AddExecutionServices_RegistersOnlyCanonicalMcpDataPlaneHost()
    {
        var services = new ServiceCollection();

        services.AddExecutionServices();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(HostMcpServerHostedService));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(DevToolsPipeServer));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IBridgeRequestHandler));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(PytestDependencyService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(PytestExecutionService));
    }
}

[Collection(HostMcpServerPipeCollection.Name)]
public sealed class McpNamedPipeIntegrationTests
{
    [Fact]
    public async Task InstanceManager_RetriesAdvertisedPipeAfterInitialConnectionFailure()
    {
        var pipeName = HostPipeName.Format("Revit", "2027", 4217);
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
        var pipeName = HostPipeName.Format("Revit", "2027", 4218);
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
        var hostInfo = new TestHostAppInfo();
        var tools = new McpServerPrimitiveCollection<McpServerTool>
        {
            new TestTool("session_test")
        };
        var optionsFactory = new HostMcpServerOptionsFactory(hostInfo, tools, [], []);
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        await using var hostedService = new HostMcpServerHostedService(
            optionsFactory,
            hostInfo,
            NullLoggerFactory.Instance,
            serviceProvider);

        await hostedService.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await using var session = await HostMcpSession.ConnectAsync(
                PipeName(hostInfo),
                generation: 1,
                NullLoggerFactory.Instance,
                TestContext.Current.CancellationToken);

            Assert.Equal(
                new HostInstanceDescriptor(Environment.ProcessId, "Revit", "2027", PipeName(hostInfo)),
                session.Instance);
            Assert.Equal(1, session.Generation);

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
        var hostInfo = new TestHostAppInfo();
        var optionsFactory = new HostMcpServerOptionsFactory(hostInfo, [], [], []);
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        await using var hostedService = new HostMcpServerHostedService(
            optionsFactory,
            hostInfo,
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
            hostInfo,
            NullLoggerFactory.Instance,
            serviceProvider);

        await hostedService.StartAsync(CancellationToken.None);
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                PipeName(hostInfo),
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
    public async Task HostMcpSession_RejectsServerMetadataThatDoesNotMatchPipeIdentity()
    {
        var pipeIdentity = new TestHostAppInfo();
        var advertisedIdentity = new MismatchedHostAppInfo();
        var optionsFactory = new HostMcpServerOptionsFactory(advertisedIdentity, [], [], []);
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        await using var hostedService = new HostMcpServerHostedService(
            optionsFactory,
            pipeIdentity,
            NullLoggerFactory.Instance,
            serviceProvider);

        await hostedService.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var pipeName = PipeName(pipeIdentity);
            var exception = await Assert.ThrowsAsync<HostIdentityException>(() =>
                HostMcpSession.ConnectAsync(
                    pipeName,
                    generation: 7,
                    NullLoggerFactory.Instance,
                    TestContext.Current.CancellationToken));

            Assert.Equal("host_identity_mismatch", exception.Code);
            Assert.Equal(pipeName, exception.PipeName);
            Assert.DoesNotContain("Rhino", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("8.0", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            await hostedService.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task HostedService_KeepsConcurrentMcpClientsIsolated()
    {
        var hostInfo = new TestHostAppInfo();
        var blockingTool = new ConcurrentClientTool();
        var optionsFactory = new HostMcpServerOptionsFactory(hostInfo, [blockingTool], [], []);
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        await using var hostedService = new HostMcpServerHostedService(
            optionsFactory,
            hostInfo,
            NullLoggerFactory.Instance,
            serviceProvider);

        await hostedService.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var pipeName = PipeName(hostInfo);
            var first = await TestClientConnection.ConnectAsync(pipeName, TestContext.Current.CancellationToken);
            await using var second = await TestClientConnection.ConnectAsync(pipeName, TestContext.Current.CancellationToken);
            try
            {
                Assert.Equal(second.Client.ServerInfo.Name, first.Client.ServerInfo.Name);
                Assert.Equal(second.Client.ServerInfo.Version, first.Client.ServerInfo.Version);
                Assert.Equal("Revit", first.Client.ServerInfo.Name);
                Assert.Equal("2027", first.Client.ServerInfo.Version);

                var firstCall = first.Client.CallToolAsync(
                    "concurrent_client_test",
                    cancellationToken: CancellationToken.None).AsTask();
                var secondCall = second.Client.CallToolAsync(
                    "concurrent_client_test",
                    cancellationToken: TestContext.Current.CancellationToken).AsTask();
                await blockingTool.BothEntered.Task.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);

                await first.DisposeAsync();
                blockingTool.Release.TrySetResult(true);

                var result = await secondCall.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
                Assert.Equal("second client remains connected", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
                await Assert.ThrowsAnyAsync<Exception>(() => firstCall);
            }
            finally
            {
                await first.DisposeAsync();
                blockingTool.Release.TrySetResult(true);
            }
        }
        finally
        {
            await hostedService.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task CaseEvents_DoNotCrossConcurrentSessions()
    {
        var hostInfo = new TestHostAppInfo();
        var tool = new PytestRunTool(
            new ImmediateHostContextExecutor(),
            new ReadyDependencyService(),
            new CaseEventExecutionService(),
            NullLogger<PytestRunTool>.Instance);
        var optionsFactory = new HostMcpServerOptionsFactory(hostInfo, [tool.Primitive], [], []);
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        await using var hostedService = new HostMcpServerHostedService(
            optionsFactory,
            hostInfo,
            NullLoggerFactory.Instance,
            serviceProvider);

        await hostedService.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var pipeName = PipeName(hostInfo);
            var firstCases = new List<string>();
            var secondCases = new List<string>();
            await using var first = await TestClientConnection.ConnectAsync(
                pipeName,
                CaseEventOptions(firstCases),
                TestContext.Current.CancellationToken);
            await using var second = await TestClientConnection.ConnectAsync(
                pipeName,
                CaseEventOptions(secondCases),
                TestContext.Current.CancellationToken);

            await Task.WhenAll(
                RunPytestAsync(first.Client, "first", TestContext.Current.CancellationToken),
                RunPytestAsync(second.Client, "second", TestContext.Current.CancellationToken));

            Assert.Equal(["first"], firstCases);
            Assert.Equal(["second"], secondCases);
        }
        finally
        {
            await hostedService.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task HostMcpSession_PreCancelledConnectFailsImmediately()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HostMcpSession.ConnectAsync(
                HostPipeName.Format("Revit", "2027", Environment.ProcessId + 10_000),
                generation: 1,
                NullLoggerFactory.Instance,
                cancelled.Token));
    }

    [Fact]
    public async Task HostMcpSession_InFlightCallCompletesOnHostShutdownAndFreshSessionReacquiresPipe()
    {
        var blockingTool = new BlockingTool();
        var hostInfo = new TestHostAppInfo();
        var optionsFactory = new HostMcpServerOptionsFactory(hostInfo, [blockingTool], [], []);
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        await using var firstHost = new HostMcpServerHostedService(optionsFactory, hostInfo, NullLoggerFactory.Instance, serviceProvider);
        await firstHost.StartAsync(TestContext.Current.CancellationToken);
        var firstSession = await HostMcpSession.ConnectAsync(PipeName(hostInfo), generation: 1, NullLoggerFactory.Instance, TestContext.Current.CancellationToken);
        try
        {
            var pendingCall = firstSession.CallToolAsync("blocking_session_test", null, CancellationToken.None);
            await blockingTool.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            var shutdown = firstHost.StopAsync(TestContext.Current.CancellationToken);
            var completed = await Task.WhenAny(pendingCall, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Same(pendingCall, completed);
            await Assert.ThrowsAnyAsync<Exception>(() => pendingCall);
            await shutdown.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            await firstSession.DisposeAsync();
        }

        var replacementOptions = new HostMcpServerOptionsFactory(hostInfo, [new TestTool("reconnected_session_test")], [], []);
        await using var secondHost = new HostMcpServerHostedService(replacementOptions, hostInfo, NullLoggerFactory.Instance, serviceProvider);
        await secondHost.StartAsync(TestContext.Current.CancellationToken);
        await using var reconnected = await HostMcpSession.ConnectAsync(PipeName(hostInfo), generation: 1, NullLoggerFactory.Instance, TestContext.Current.CancellationToken);
        Assert.True(reconnected.IsConnected);
        var result = await reconnected.CallToolAsync("reconnected_session_test", null, TestContext.Current.CancellationToken);
        Assert.Equal("typed session", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        await secondHost.StopAsync(TestContext.Current.CancellationToken);
    }

    private sealed class TestHostAppInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber => "2027";
        public string? VersionBuild => null;
        public int ProcessId => Environment.ProcessId;
    }

    private sealed class MismatchedHostAppInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Rhino;
        public string VersionNumber => "8.0";
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

    private sealed class BlockingTool : McpServerTool
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override Tool ProtocolTool { get; } = new()
        {
            Name = "blocking_session_test",
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object" })
        };

        public override IReadOnlyList<object> Metadata => [];

        public override async ValueTask<CallToolResult> InvokeAsync(
            RequestContext<CallToolRequestParams> request,
            CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The blocking request should be cancelled by host shutdown.");
        }
    }

    private sealed class ConcurrentClientTool : McpServerTool
    {
        private int _enteredCount;

        public TaskCompletionSource<bool> BothEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override Tool ProtocolTool { get; } = new()
        {
            Name = "concurrent_client_test",
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object" })
        };

        public override IReadOnlyList<object> Metadata => [];

        public override async ValueTask<CallToolResult> InvokeAsync(
            RequestContext<CallToolRequestParams> request,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _enteredCount) == 2)
                BothEntered.TrySetResult(true);

            await Release.Task.WaitAsync(cancellationToken);
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = "second client remains connected" }]
            };
        }
    }

    private sealed class TestClientConnection(NamedPipeClientStream pipe, McpClient client) : IAsyncDisposable
    {
        private int _disposed;

        public McpClient Client { get; } = client;

        public static async Task<TestClientConnection> ConnectAsync(
            string pipeName,
            CancellationToken ct) =>
            await ConnectAsync(pipeName, null, ct);

        public static async Task<TestClientConnection> ConnectAsync(
            string pipeName,
            McpClientOptions? options,
            CancellationToken ct)
        {
            var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            McpClient? client = null;
            try
            {
                await pipe.ConnectAsync(5000, ct);
                var transport = new StreamClientTransport(pipe, pipe, NullLoggerFactory.Instance);
                client = await McpClient.CreateAsync(
                    transport,
                    options ?? new McpClientOptions
                    {
                        ClientInfo = new Implementation { Name = "integration-test", Version = "1.0" }
                    },
                    cancellationToken: ct);
                return new TestClientConnection(pipe, client);
            }
            catch
            {
                if (client is not null)
                    await client.DisposeAsync();
                await pipe.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            await Client.DisposeAsync();
            await pipe.DisposeAsync();
        }
    }

    private static McpClientOptions CaseEventOptions(ICollection<string> cases) => new()
    {
        ClientInfo = new Implementation { Name = "case-event-client", Version = "1.0" },
        Capabilities = new ClientCapabilities
        {
            Experimental = new Dictionary<string, object>
            {
                ["devtools"] = JsonSerializer.SerializeToElement(new
                {
                    pytest = new { caseEvents = new { version = "1" } }
                })
            }
        },
        Handlers = new McpClientHandlers
        {
            NotificationHandlers = new Dictionary<string, Func<JsonRpcNotification, CancellationToken, ValueTask>>
            {
                ["notifications/devtools/pytest/case"] = (notification, _) =>
                {
                    cases.Add(notification.Params!["case"]!["nodeid"]!.GetValue<string>());
                    return ValueTask.CompletedTask;
                }
            }
        }
    };

    private static Task<CallToolResult> RunPytestAsync(McpClient client, string nodeId, CancellationToken cancellationToken) =>
        client.CallToolAsync(
            new CallToolRequestParams
            {
                Name = "pytest_run",
                Meta = new JsonObject { ["progressToken"] = nodeId },
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["workspace_root"] = JsonSerializer.SerializeToElement(Path.GetTempPath()),
                    ["test_root"] = JsonSerializer.SerializeToElement(Path.GetTempPath()),
                    ["nodeids"] = JsonSerializer.SerializeToElement(new[] { nodeId }),
                    ["pytest_args"] = JsonSerializer.SerializeToElement(Array.Empty<string>())
                }
            },
            cancellationToken).AsTask();

    private sealed class ImmediateHostContextExecutor : IHostContextExecutor
    {
        public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(handler());
        }

        public Task ExecuteAsync(Action action, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class ReadyDependencyService : PytestDependencyService
    {
        public ReadyDependencyService() : base(null!) { }

        public override Task PrepareRunAsync(PytestRunRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class CaseEventExecutionService : PytestExecutionService
    {
        public CaseEventExecutionService() : base(null!) { }

        public override PytestRunResponse Run(
            PytestRunRequest request,
            Action<string>? progressCallback,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progressCallback?.Invoke(JsonSerializer.Serialize(new PytestCaseResult(
                request.NodeIds[0], "passed", "call", 1, "", "", "", "")));
            return new PytestRunResponse(
                0,
                new PytestSummary(1, 0, 0, 0, 0, 0),
                [],
                [],
                request.TestRoot);
        }
    }

    private static string PipeName(IHostAppInfo hostInfo) =>
        HostPipeName.Format(hostInfo.Host.ToString(), hostInfo.VersionNumber, Environment.ProcessId);

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
        public Task<IHostMcpSession> ConnectAsync(string pipeName, int generation, CancellationToken ct) => connectAsync(pipeName, ct);
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
        public int Generation { get; init; } = 1;
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
