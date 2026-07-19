using DevTools.Daemon.Contracts;
using DevTools.Daemon.Hosts;
using DevTools.Daemon.Mcp;
using DevTools.Daemon.Mcp.Tools;
using DevTools.Ipc;
using DevTools.Logging;
using DevTools.Mcp.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RevitDevTool.Server.Tests;

public sealed class HostDriverRegistryTests
{
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
        var driver = new CapturingHostDriver(9002, HostApp.Revit);
        var tool = new LaunchHostTool(instanceManager, new HostDriverRegistry([driver]));

        try
        {
            var result = await tool.LaunchAsync("Revit", "2025", "FRA", filePath, TestContext.Current.CancellationToken);

            Assert.False(result.IsError ?? false);
            Assert.Equal(new HostLaunchRequest(HostApp.Revit, "2025", "FRA", filePath), driver.Request);
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
        var driver = new CapturingHostDriver(9003, HostApp.Civil3D);
        var tool = new LaunchHostTool(instanceManager, new HostDriverRegistry([driver]));

        var result = await tool.LaunchAsync("Civil3D", "2025", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsError ?? false);
        Assert.Equal(HostApp.Civil3D, driver.Request?.RequestedHostApp);
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
                null));
        }

        public Task<FileInfoResult> ReadFileInfoAsync(string filePath, CancellationToken ct) =>
            throw new NotSupportedException();
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
        public Task<IHostMcpSession> ConnectAsync(string pipeName, CancellationToken ct) => connectAsync(pipeName, ct);
    }

    private sealed class ImmediateRetryClock : IRetryClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public Task DelayAsync(TimeSpan delay, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class CapturingSession(int processId) : IHostMcpSession
    {
        public HostInstanceDescriptor Instance { get; } = new(processId, "TestHost", "1.0", McpPipeName.Format(processId));
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
