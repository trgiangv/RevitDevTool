using DevTools.Daemon.Contracts;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Hosting;
using DevTools.Daemon.Hosts;
using DevTools.Daemon.Mcp;
using DevTools.Daemon.Mcp.Tools;
using DevTools.Daemon.Mcp.Tools.Utils;
using DevTools.Ipc;
using DevTools.Logging;
using DevTools.Mcp;
using DevTools.Mcp.Routing;
using DevTools.Mcp.Routing.Broker;
using DevTools.Mcp.Routing.Catalog;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;

namespace RevitDevTool.Server.Tests;

public sealed class HostDriverRegistryTests
{
    [Fact]
    public void DaemonComposition_ResolvesCatalogCoordinatorBeforeEngineWithoutCycle()
    {
        using var host = DaemonHostBuilder.CreateStdioHost([]);

        var coordinator = host.Services.GetRequiredService<HostCatalogCoordinator>();
        var engine = host.Services.GetRequiredService<McpEngine>();

        Assert.NotNull(coordinator);
        Assert.NotNull(engine);
    }

    [Fact]
    public async Task DiscoveryConnection_EagerlyPublishesFirstCatalogThroughCoordinatorSubscription()
    {
        var session = new CapturingSession(9008);
        var manager = new HostSessionManager(
            NullLogger<HostSessionManager>.Instance,
            NullLoggerFactory.Instance,
            _ => [session.Instance.PipeName],
            new DelegateHostSessionConnector((_, _) => Task.FromResult<IHostMcpSession>(session)),
            new BlockingRetryClock());
        await using var ownedManager = manager;
        using var services = new ServiceCollection()
            .AddSingleton(new HostDriverRegistry([]))
            .BuildServiceProvider();
        var engine = new McpEngine(
            manager,
            new BrokerCatalogIndex(),
            new TestAuthService(),
            Options.Create(new GatewayOptions()),
            services);
        await using var coordinator = new HostCatalogCoordinator(
            engine,
            new DaemonSettings(),
            NullLogger<CatalogService>.Instance,
            NullLogger<HostCatalogCoordinator>.Instance);
        var discovery = new DiscoveryHostedService(manager, coordinator);

        await discovery.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            for (var attempt = 0; attempt < 100 && manager.GetSession(9008, 1) is null; attempt++)
                await Task.Delay(10, TestContext.Current.CancellationToken);

            var state = await coordinator.WaitForFirstFetchAsync(
                session.Instance.ProcessId,
                session.Generation,
                TimeSpan.FromSeconds(1),
                TestContext.Current.CancellationToken);

            Assert.Equal(HostCatalogState.Ready, state);
            Assert.Same(session, manager.GetSession(session.Instance.ProcessId, session.Generation));
        }
        finally
        {
            await discovery.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData("model.rvt", typeof(RevitHostDriver))]
    [InlineData("family.rfa", typeof(RevitHostDriver))]
    [InlineData("drawing.dwg", typeof(AcadHostDriver))]
    [InlineData("template.dwt", typeof(AcadHostDriver))]
    [InlineData("template.rft", typeof(RevitHostDriver))]
    [InlineData("project.rte", typeof(RevitHostDriver))]
    [InlineData("exchange.dxf", typeof(AcadHostDriver))]
    [InlineData("published.dwf", typeof(AcadHostDriver))]
    [InlineData("MODEL.RVT", typeof(RevitHostDriver))]
    [InlineData("TEMPLATE.DWT", typeof(AcadHostDriver))]
    public void ForFile_selects_the_driver_registered_for_the_file_extension(string fileName, Type expectedDriver)
    {
        var registry = new HostDriverRegistry([new RevitHostDriver(), new AcadHostDriver()]);

        Assert.IsType(expectedDriver, registry.ForFile(fileName));
    }

    [Fact]
    public void TryForFile_returns_null_when_no_driver_owns_the_extension()
    {
        var registry = new HostDriverRegistry([new RevitHostDriver(), new AcadHostDriver()]);

        Assert.Null(registry.TryForFile("notes.txt"));
    }

    [Fact]
    public void Constructor_rejects_duplicate_extensions_without_regard_to_case()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new HostDriverRegistry(
        [
            new TestHostDriver("first", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".abc" }),
            new TestHostDriver("second", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ABC" })
        ]));

        Assert.Contains(".abc", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("first", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("second", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(HostApp.AutoCad)]
    [InlineData(HostApp.Civil3D)]
    [InlineData(HostApp.Plant3D)]
    [InlineData(HostApp.AcadArch)]
    [InlineData(HostApp.AcadMech)]
    [InlineData(HostApp.AcadElec)]
    [InlineData(HostApp.AcadMep)]
    [InlineData(HostApp.AcadMap3D)]
    public void ForHost_selects_the_same_AutoCAD_family_driver_for_every_supported_product(HostApp hostApp)
    {
        var acadDriver = new AcadHostDriver();
        var registry = new HostDriverRegistry([new RevitHostDriver(), acadDriver]);

        Assert.Same(acadDriver, registry.ForHost(hostApp));
    }

    [Fact]
    public void ForHost_selects_the_Revit_driver_for_Revit()
    {
        var registry = new HostDriverRegistry([new RevitHostDriver(), new AcadHostDriver()]);

        Assert.IsType<RevitHostDriver>(registry.ForHost(HostApp.Revit));
    }

    [Theory]
    [InlineData(HostApp.Navisworks)]
    [InlineData(HostApp.Rhino)]
    [InlineData(HostApp.Tekla)]
    public void ForHost_returns_null_when_no_driver_supports_the_product(HostApp hostApp)
    {
        var registry = new HostDriverRegistry([new RevitHostDriver(), new AcadHostDriver()]);

        Assert.Null(registry.TryForHost(hostApp));
    }

    [Fact]
    public async Task OpenModel_forwards_a_connected_Navisworks_file_without_requiring_a_launch_driver()
    {
        var filePath = CreateTemporaryFile(".nwd");
        var session = new CapturingSession(9001);
        await using var instanceManager = await CreateHostSessionManagerAsync([session]);
        var tool = new OpenModelTool(instanceManager, new HostDriverRegistry([]));

        try
        {
            var result = await tool.OpenAsync(filePath, cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.IsError ?? false);
            var call = Assert.Single(session.ToolCalls);
            Assert.Equal("open_document", call.Name);
            Assert.Equal(filePath, call.Arguments["filePath"]);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task LaunchHost_forwards_Revit_language_version_and_file_path_to_the_selected_driver()
    {
        var filePath = CreateTemporaryFile(".rvt");
        var session = new CapturingSession(9002);
        await using var instanceManager = await CreateHostSessionManagerAsync([session]);
        await using var coordinator = CreateCoordinatorWithStatus(instanceManager, session, HostCatalogState.Ready);
        var driver = new CapturingHostDriver(9002, HostApp.Revit);
        var tool = new LaunchHostTool(instanceManager, new HostDriverRegistry([driver]), coordinator);

        try
        {
            var result = await tool.LaunchAsync("Revit", "2025", "FRA", filePath, TestContext.Current.CancellationToken);

            Assert.False(result.IsError ?? false);
            Assert.Equal(new HostLaunchRequest(HostApp.Revit, "2025", "FRA", filePath), driver.Request);
            Assert.Equal(LaunchHostStatus.ConnectedCatalogReady, ReadPayload(result).GetProperty("status").GetString());
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task LaunchHost_preserves_the_requested_Civil3D_product_for_the_selected_driver()
    {
        var session = new CapturingSession(9003);
        await using var instanceManager = await CreateHostSessionManagerAsync([session]);
        await using var coordinator = CreateCoordinatorWithStatus(instanceManager, session, HostCatalogState.Ready);
        var driver = new CapturingHostDriver(9003, HostApp.Civil3D);
        var tool = new LaunchHostTool(instanceManager, new HostDriverRegistry([driver]), coordinator);

        var result = await tool.LaunchAsync("Civil3D", "2025", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsError ?? false);
        Assert.Equal(HostApp.Civil3D, driver.Request?.RequestedHostApp);
    }

    [Fact]
    public async Task Launch_ReturnsPendingWhenFirstFetchExceedsTenSeconds()
    {
        var session = new CapturingSession(9004);
        await using var instanceManager = await CreateHostSessionManagerAsync([session]);
        await using var coordinator = new HostCatalogCoordinator(_ => Task.CompletedTask, instanceManager);
        var driver = new CapturingHostDriver(9004, HostApp.Revit);
        var tool = new LaunchHostTool(
            instanceManager,
            new HostDriverRegistry([driver]),
            coordinator,
            catalogTimeout: TimeSpan.FromMilliseconds(10));

        var result = await tool.LaunchAsync("Revit", "2025", cancellationToken: TestContext.Current.CancellationToken);
        var payload = ReadPayload(result);

        Assert.Equal(LaunchHostStatus.ConnectedCatalogPending, payload.GetProperty("status").GetString());
        Assert.Equal(9004, payload.GetProperty("processId").GetInt32());
        Assert.Contains(
            "Call devtools_search again; the host catalog is still refreshing.",
            payload.GetProperty("message").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Launch_StartsCatalogBarrierBeforeDialogSnapshotCompletes()
    {
        var session = new CapturingSession(9009);
        await using var instanceManager = await CreateHostSessionManagerAsync([session]);
        var readinessStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readinessSessions = new SignalingInstanceManager(session, readinessStarted);
        await using var coordinator = new HostCatalogCoordinator(_ => Task.CompletedTask, readinessSessions);
        var dialogRelease = new TaskCompletionSource<StartupDialogResolverResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var driver = new CapturingHostDriver(9009, HostApp.Revit) { DialogTask = dialogRelease.Task };
        var tool = new LaunchHostTool(
            instanceManager,
            new HostDriverRegistry([driver]),
            coordinator,
            catalogTimeout: TimeSpan.FromSeconds(5));
        var launch = tool.LaunchAsync("Revit", "2025", cancellationToken: TestContext.Current.CancellationToken);

        try
        {
            await readinessStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            coordinator.PublishStatus(
                new HostCatalogIdentity(session.Instance.PipeName, session.Generation),
                HostCatalogState.Ready);
            dialogRelease.TrySetResult(new StartupDialogResolverResult(TimedOut: false, Events: []));

            var result = await launch;

            Assert.Equal(LaunchHostStatus.ConnectedCatalogReady, ReadPayload(result).GetProperty("status").GetString());
        }
        finally
        {
            coordinator.PublishStatus(
                new HostCatalogIdentity(session.Instance.PipeName, session.Generation),
                HostCatalogState.Ready);
            dialogRelease.TrySetResult(new StartupDialogResolverResult(TimedOut: false, Events: []));
            try
            {
                await launch.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task Launch_PropagatesCallerCancellationAfterTerminalCatalogWhileDialogIsPending()
    {
        var session = new CapturingSession(9010);
        await using var instanceManager = await CreateHostSessionManagerAsync([session]);
        await using var coordinator = CreateCoordinatorWithStatus(instanceManager, session, HostCatalogState.Ready);
        var dialogRelease = new TaskCompletionSource<StartupDialogResolverResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var driver = new CapturingHostDriver(9010, HostApp.Revit) { DialogTask = dialogRelease.Task };
        var tool = new LaunchHostTool(
            instanceManager,
            new HostDriverRegistry([driver]),
            coordinator,
            catalogTimeout: TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        var launch = tool.LaunchAsync("Revit", "2025", cancellationToken: cancellation.Token);

        try
        {
            Assert.False(launch.IsCompleted);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => launch.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
        }
        finally
        {
            dialogRelease.TrySetResult(new StartupDialogResolverResult(TimedOut: false, Events: []));
            try
            {
                await launch.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task Launch_ReturnsLaunchFailedWhenDriverCannotStartProcess()
    {
        await using var instanceManager = await CreateHostSessionManagerAsync([]);
        await using var coordinator = new HostCatalogCoordinator(_ => Task.CompletedTask, instanceManager);
        var tool = new LaunchHostTool(
            instanceManager,
            new HostDriverRegistry([new FailingHostDriver()]),
            coordinator);

        var result = await tool.LaunchAsync("Revit", "2025", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(LaunchHostStatus.LaunchFailed, ReadPayload(result).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Launch_ReturnsConnectionTimeoutWhenExactSessionDoesNotConnect()
    {
        await using var instanceManager = await CreateHostSessionManagerAsync([]);
        await using var coordinator = new HostCatalogCoordinator(_ => Task.CompletedTask, instanceManager);
        var driver = new CapturingHostDriver(9005, HostApp.Revit);
        var tool = new LaunchHostTool(
            instanceManager,
            new HostDriverRegistry([driver]),
            coordinator,
            connectionTimeout: TimeSpan.FromMilliseconds(10),
            connectionPollInterval: TimeSpan.FromMilliseconds(1));

        var result = await tool.LaunchAsync("Revit", "2025", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(LaunchHostStatus.ConnectionTimeout, ReadPayload(result).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Launch_PropagatesCallerCancellationWhileWaitingForConnection()
    {
        await using var instanceManager = await CreateHostSessionManagerAsync([]);
        await using var coordinator = new HostCatalogCoordinator(_ => Task.CompletedTask, instanceManager);
        var driver = new CapturingHostDriver(9007, HostApp.Revit);
        var tool = new LaunchHostTool(
            instanceManager,
            new HostDriverRegistry([driver]),
            coordinator,
            connectionTimeout: TimeSpan.FromSeconds(10),
            connectionPollInterval: TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => tool.LaunchAsync("Revit", "2025", cancellationToken: cancellation.Token));
    }

    [Theory]
    [InlineData(HostCatalogState.Stale, LaunchHostStatus.ConnectedCatalogReady)]
    [InlineData(HostCatalogState.Unavailable, LaunchHostStatus.ConnectedCatalogPending)]
    public async Task Launch_MapsTerminalCatalogStateToUsability(
        HostCatalogState catalogState,
        string expectedStatus)
    {
        var session = new CapturingSession(9006);
        await using var instanceManager = await CreateHostSessionManagerAsync([session]);
        await using var coordinator = CreateCoordinatorWithStatus(instanceManager, session, catalogState);
        var driver = new CapturingHostDriver(9006, HostApp.Revit);
        var tool = new LaunchHostTool(instanceManager, new HostDriverRegistry([driver]), coordinator);

        var result = await tool.LaunchAsync("Revit", "2025", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, ReadPayload(result).GetProperty("status").GetString());
    }

    private sealed class TestHostDriver(string hostId, IReadOnlySet<string> fileExtensions) : IHostDriver
    {
        public string HostId => hostId;
        public IReadOnlySet<HostApp> SupportedHostApps { get; } = new HashSet<HostApp>();
        public IReadOnlySet<string> FileExtensions => fileExtensions;
        public bool SupportsVersion(string version) => true;
        public Task<HostLaunchResult> LaunchAsync(HostLaunchRequest request, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<FileInfoResult> ReadFileInfoAsync(string filePath, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingHostDriver(int processId, params HostApp[] supportedHostApps) : IHostDriver
    {
        public string HostId => "Revit";
        public IReadOnlySet<HostApp> SupportedHostApps { get; } = new HashSet<HostApp>(supportedHostApps);
        public IReadOnlySet<string> FileExtensions { get; } = new HashSet<string> { ".rvt" };
        public HostLaunchRequest? Request { get; private set; }
        public Task<StartupDialogResolverResult>? DialogTask { get; init; }
        public bool SupportsVersion(string version) => true;

        public Task<HostLaunchResult> LaunchAsync(HostLaunchRequest request, CancellationToken ct)
        {
            Request = request;
            return Task.FromResult(new HostLaunchResult(
                request.RequestedHostApp,
                processId,
                request.VersionNumber ?? "2025",
                "C:\\test-host.exe",
                request.LanguageCode,
                [],
                DialogTask));
        }

        public Task<FileInfoResult> ReadFileInfoAsync(string filePath, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class FailingHostDriver : IHostDriver
    {
        public string HostId => "failing";
        public IReadOnlySet<HostApp> SupportedHostApps { get; } = new HashSet<HostApp> { HostApp.Revit };
        public IReadOnlySet<string> FileExtensions { get; } = new HashSet<string> { ".rvt" };
        public bool SupportsVersion(string version) => true;
        public Task<HostLaunchResult> LaunchAsync(HostLaunchRequest request, CancellationToken ct) =>
            throw new HostDriverException("launch failed");
        public Task<FileInfoResult> ReadFileInfoAsync(string filePath, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private static HostCatalogCoordinator CreateCoordinatorWithStatus(
        HostSessionManager instanceManager,
        IHostMcpSession session,
        HostCatalogState state)
    {
        var coordinator = new HostCatalogCoordinator(_ => Task.CompletedTask, instanceManager);
        coordinator.RequestRefresh();
        coordinator.PublishStatus(
            new HostCatalogIdentity(session.Instance.PipeName, session.Generation),
            state);
        return coordinator;
    }

    private static JsonElement ReadPayload(CallToolResult result)
    {
        var content = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        using var document = JsonDocument.Parse(content.Text);
        return document.RootElement.Clone();
    }

    private static async Task<HostSessionManager> CreateHostSessionManagerAsync(IReadOnlyList<CapturingSession> sessions)
    {
        var sessionsByPipe = sessions.ToDictionary(session => session.Instance.PipeName, StringComparer.OrdinalIgnoreCase);
        var instanceManager = new HostSessionManager(
            NullLogger<HostSessionManager>.Instance,
            NullLoggerFactory.Instance,
            _ => [.. sessionsByPipe.Keys],
            new DelegateHostSessionConnector((pipeName, _) => Task.FromResult<IHostMcpSession>(sessionsByPipe[pipeName])),
            new ImmediateRetryClock());

        await instanceManager.SyncMcpPipesAsync(TestContext.Current.CancellationToken);
        return instanceManager;
    }

    private static string CreateTemporaryFile(string extension)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
        File.WriteAllText(filePath, string.Empty);
        return filePath;
    }

    private sealed class DelegateHostSessionConnector(
        Func<string, CancellationToken, Task<IHostMcpSession>> connectAsync) : IHostSessionConnector
    {
        public Task<IHostMcpSession> ConnectAsync(string pipeName, int generation, CancellationToken ct) => connectAsync(pipeName, ct);
    }

    private sealed class ImmediateRetryClock : IRetryClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public Task DelayAsync(TimeSpan delay, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class BlockingRetryClock : IRetryClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public Task DelayAsync(TimeSpan delay, CancellationToken ct) => Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }

    private sealed class SignalingInstanceManager(
        IHostMcpSession session,
        TaskCompletionSource<bool> readinessStarted) : IInstanceManager
    {
        public IReadOnlyCollection<IHostMcpSession> Sessions => [session];
        public event Action? SessionsChanged { add { } remove { } }

        public IHostMcpSession? GetSessionByProcessId(int processId) =>
            processId == session.Instance.ProcessId ? session : null;

        public IHostMcpSession? GetSession(int processId, int generation)
        {
            readinessStarted.TrySetResult(true);
            return processId == session.Instance.ProcessId && generation == session.Generation
                ? session
                : null;
        }
    }

    private sealed class TestAuthService : IAuthService
    {
        public bool IsAuthenticated => false;
        public string? AccessToken => null;
        public string? UserId => null;
        public string? Email => null;
        public string? DisplayName => null;
        public string? AvatarUrl => null;
        public event EventHandler<AuthStateChangedArgs>? StateChanged { add { } remove { } }
        public Task<AuthResult> SignInAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task SignOutAsync() => throw new NotSupportedException();
        public Task<bool> RefreshAsync() => throw new NotSupportedException();
    }

    private sealed class CapturingSession(int processId) : IHostMcpSession
    {
        public HostInstanceDescriptor Instance { get; } = new(processId, "TestHost", "1.0", McpPipeName.Format(processId));
        public int Generation { get; init; } = 1;
        public bool IsConnected => true;
        public List<(string Name, IReadOnlyDictionary<string, object?> Arguments)> ToolCalls { get; } = [];
        public event Action? CatalogChanged { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }

        public Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct) => Task.FromResult<IList<McpClientTool>>([]);
        public Task<IList<McpClientPrompt>> ListPromptsAsync(CancellationToken ct) => Task.FromResult<IList<McpClientPrompt>>([]);
        public Task<IList<McpClientResource>> ListResourcesAsync(CancellationToken ct) => Task.FromResult<IList<McpClientResource>>([]);
        public Task<IList<McpClientResourceTemplate>> ListResourceTemplatesAsync(CancellationToken ct) => Task.FromResult<IList<McpClientResourceTemplate>>([]);

        public Task<CallToolResult> CallToolAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct)
        {
            ToolCalls.Add((name, arguments ?? new Dictionary<string, object?>()));
            return Task.FromResult(new CallToolResult { Content = [new TextContentBlock { Text = "opened" }] });
        }

        public Task<GetPromptResult> GetPromptAsync(string name, IReadOnlyDictionary<string, object?>? arguments, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<ReadResourceResult> ReadResourceAsync(string uri, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
