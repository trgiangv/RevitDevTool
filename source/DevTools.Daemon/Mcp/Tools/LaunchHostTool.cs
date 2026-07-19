using System.ComponentModel;
using System.Runtime.Versioning;
using System.Text.Json;
using DevTools.Daemon.Contracts;
using DevTools.Daemon.Hosts;
using DevTools.Logging;
using DevTools.Daemon.Mcp.Tools.Utils;
using DevTools.Mcp;
using DevTools.Mcp.Routing.Catalog;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

[SupportedOSPlatform("windows")]
[McpServerToolType]
public sealed class LaunchHostTool
{
    private static readonly TimeSpan DefaultConnectionTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DefaultConnectionPollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DefaultCatalogTimeout = TimeSpan.FromSeconds(10);
    private readonly HostSessionManager instanceManager;
    private readonly HostDriverRegistry drivers;
    private readonly Func<HostCatalogCoordinator> getCatalogCoordinator;
    private readonly TimeSpan connectionTimeout;
    private readonly TimeSpan connectionPollInterval;
    private readonly TimeSpan catalogTimeout;

    internal LaunchHostTool(
        HostSessionManager instanceManager,
        HostDriverRegistry drivers,
        Func<HostCatalogCoordinator> getCatalogCoordinator)
    {
        this.instanceManager = instanceManager;
        this.drivers = drivers;
        this.getCatalogCoordinator = getCatalogCoordinator;
        connectionTimeout = DefaultConnectionTimeout;
        connectionPollInterval = DefaultConnectionPollInterval;
        catalogTimeout = DefaultCatalogTimeout;
    }

    internal LaunchHostTool(
        HostSessionManager instanceManager,
        HostDriverRegistry drivers,
        HostCatalogCoordinator catalogCoordinator,
        TimeSpan? catalogTimeout = null,
        TimeSpan? connectionTimeout = null,
        TimeSpan? connectionPollInterval = null)
    {
        this.instanceManager = instanceManager;
        this.drivers = drivers;
        getCatalogCoordinator = () => catalogCoordinator;
        this.catalogTimeout = catalogTimeout ?? DefaultCatalogTimeout;
        this.connectionTimeout = connectionTimeout ?? DefaultConnectionTimeout;
        this.connectionPollInterval = connectionPollInterval ?? DefaultConnectionPollInterval;
    }

    private static readonly string[] HostAppEnumNames =
        Enum.GetValues<HostApp>().Select(h => h.ToString()).ToArray();

    [McpServerTool(Name = "launch_host")]
    [Description("Launch a host application and wait for the DevTools bridge to connect. This is a long-running operation, typically 30-120 seconds for cold start. Revit auto-detects the version from filePath when provided; AutoCAD-family hosts use the latest installed version unless versionNumber is specified.")]
    public async Task<CallToolResult> LaunchAsync(
        [Description("Revit, AutoCad, Civil3D, Plant3D, AcadArch, AcadMech, AcadElec, AcadMep, AcadMap3D, or Navisworks.")] string hostApp,
        [Description("Version year such as 2025. Revit can auto-detect it from filePath.")] string? versionNumber = null,
        [Description("Revit UI language code; defaults to ENU.")] string? languageCode = null,
        [Description("Optional model file to open at startup.")] string? filePath = null,
        CancellationToken cancellationToken = default)
    {
        var parsedHostApp = HostAppExtensions.ParseHostApp(hostApp);

        if (parsedHostApp is null)
            return ToolHelpers.ErrorResult($"Invalid hostApp. Supported values: {string.Join(", ", HostAppEnumNames)}");

        if (!string.IsNullOrWhiteSpace(filePath) && !File.Exists(filePath))
            return ToolHelpers.ErrorResult($"File not found: {filePath}");

        var driver = drivers.TryForHost(parsedHostApp.Value);
        if (driver is null)
            return ToolHelpers.ErrorResult($"Launch not yet supported for {parsedHostApp}.");

        HostLaunchResult launch;
        try
        {
            launch = await driver.LaunchAsync(
                new HostLaunchRequest(parsedHostApp.Value, versionNumber, languageCode, filePath), cancellationToken).ConfigureAwait(false);
        }
        catch (HostDriverException ex)
        {
            return Result(new LaunchHostResult(
                parsedHostApp.Value,
                0,
                versionNumber,
                null,
                null,
                languageCode,
                LaunchHostStatus.LaunchFailed,
                false,
                ex.Message));
        }

        var connected = await WaitForInstanceConnectionAsync(launch.ProcessId, cancellationToken).ConfigureAwait(false);
        if (connected is null)
        {
            var connectionTimeoutDialogResult = await HostLaunchCoordinator
                .TryAwaitResolverResultAsync(launch.DialogTask, TimeSpan.FromSeconds(5))
                .ConfigureAwait(false);
            return Result(new LaunchHostResult(
                parsedHostApp.Value,
                launch.ProcessId,
                launch.Version,
                launch.ExePath,
                string.Join(" ", launch.Arguments),
                launch.LanguageCode,
                LaunchHostStatus.ConnectionTimeout,
                false,
                $"{parsedHostApp} launched (PID={launch.ProcessId}) but bridge did not connect within timeout.",
                connectionTimeoutDialogResult));
        }

        var dialogResultTask = HostLaunchCoordinator.TryAwaitResolverResultAsync(
            launch.DialogTask,
            TimeSpan.FromSeconds(5));
        var catalogState = await getCatalogCoordinator()
            .WaitForFirstFetchAsync(
                launch.ProcessId,
                connected.Generation,
                catalogTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var dialogResult = await dialogResultTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        var catalogReady = catalogState is HostCatalogState.Ready or HostCatalogState.Stale;
        var status = catalogReady
            ? LaunchHostStatus.ConnectedCatalogReady
            : LaunchHostStatus.ConnectedCatalogPending;
        var message = catalogReady
            ? null
            : $"Host connected (PID={launch.ProcessId}). Call devtools_search again; the host catalog is still refreshing.";

        var payload = new LaunchHostResult(
            parsedHostApp.Value,
            launch.ProcessId,
            launch.Version,
            launch.ExePath,
            string.Join(" ", launch.Arguments),
            launch.LanguageCode,
            status,
            true,
            message,
            dialogResult);

        return Result(payload);
    }

    private async Task<IHostMcpSession?> WaitForInstanceConnectionAsync(int processId, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.Add(connectionTimeout);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (instanceManager.GetSessionByProcessId(processId) is { } session)
                return session;

            await Task.Delay(connectionPollInterval, ct).ConfigureAwait(false);
        }

        ct.ThrowIfCancellationRequested();
        return null;
    }

    private static CallToolResult Result(LaunchHostResult payload) => new()
    {
        Content = [new TextContentBlock { Text = JsonSerializer.Serialize(payload) }]
    };
}
