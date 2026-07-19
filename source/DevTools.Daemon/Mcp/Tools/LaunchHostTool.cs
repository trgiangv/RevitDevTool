using System.ComponentModel;
using System.Runtime.Versioning;
using System.Text.Json;
using DevTools.Daemon.Contracts;
using DevTools.Daemon.Hosts;
using DevTools.Logging;
using DevTools.Daemon.Mcp.Tools.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

[SupportedOSPlatform("windows")]
[McpServerToolType]
public sealed class LaunchHostTool
{
    private readonly HostSessionManager instanceManager;
    private readonly HostDriverRegistry drivers;

    internal LaunchHostTool(HostSessionManager instanceManager, HostDriverRegistry drivers)
    {
        this.instanceManager = instanceManager;
        this.drivers = drivers;
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
            return ToolHelpers.ErrorResult(ex.Message);
        }

        var connected = await WaitForInstanceConnectionAsync(launch.ProcessId, cancellationToken).ConfigureAwait(false);
        var dialogResult = await HostLaunchCoordinator.TryAwaitResolverResultAsync(launch.DialogTask, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        if (!connected)
            return ToolHelpers.ErrorResult(
                $"{parsedHostApp} launched (PID={launch.ProcessId}) but bridge did not connect within timeout.");

        var payload = new LaunchHostResult(
            parsedHostApp.Value,
            launch.ProcessId,
            launch.Version,
            launch.ExePath,
            string.Join(" ", launch.Arguments),
            launch.LanguageCode,
            true,
            dialogResult);

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(payload) }]
        };
    }

    private async Task<bool> WaitForInstanceConnectionAsync(int processId, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (instanceManager.GetSessionByProcessId(processId) is not null)
                return true;

            try { await Task.Delay(500, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return false; }
        }

        return false;
    }
}
