using System.ComponentModel;
using System.Text.Json;
using DevTools.Daemon.Contracts;
using DevTools.Daemon.Hosts;
using DevTools.Logging;
using DevTools.Daemon.Mcp.Tools.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

/// <summary>
/// Opens a model file in a host application. Two strategies:
/// 1. Connected instance: routes <c>open_document</c> built-in tool via Named Pipe.
/// 2. No instance: launches the host process with the file as a CLI argument.
/// </summary>
[McpServerToolType]
public sealed class OpenModelTool
{
    private readonly HostSessionManager instanceManager;
    private readonly HostDriverRegistry drivers;

    internal OpenModelTool(HostSessionManager instanceManager, HostDriverRegistry drivers)
    {
        this.instanceManager = instanceManager;
        this.drivers = drivers;
    }

    [McpServerTool(Name = "open_model")]
    [Description("Open a model file in a connected host or launch a new one. When launching a new host, this is a long-running operation, typically 30-120 seconds. Host is auto-detected from extension: .rvt/.rfa uses Revit and .dwg/.dxf/.dwt uses AutoCAD.")]
    public async Task<CallToolResult> OpenAsync(
        [Description("Full path to the model file.")] string filePath,
        [Description("Target host process ID when multiple instances exist.")] int? hostId = null,
        [Description("Version to launch when no compatible instance is connected.")] string? versionNumber = null,
        [Description("Revit UI language code; defaults to ENU.")] string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return ToolHelpers.ErrorResult("filePath is required.");
        if (!File.Exists(filePath))
            return ToolHelpers.ErrorResult($"File not found: {filePath}");

        var hostApp = HostAppExtensions.FromExtension(Path.GetExtension(filePath));
        var session = ResolveSession(hostId);
        if (session is not null)
            return await OpenViaConnectedInstanceAsync(session, filePath, cancellationToken).ConfigureAwait(false);

        var driver = drivers.TryForFile(filePath);
        if (driver is null)
        {
            if (hostApp is not null)
                return ToolHelpers.ErrorResult($"Launch not yet supported for {hostApp}.");

            return ToolHelpers.ErrorResult($"Cannot determine host application from file extension '{Path.GetExtension(filePath)}'.");
        }

        if (hostApp is null)
            return ToolHelpers.ErrorResult($"Cannot determine host application from file extension '{Path.GetExtension(filePath)}'.");

        return await LaunchAndOpenAsync(driver, hostApp.Value, filePath, versionNumber, languageCode, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<CallToolResult> LaunchAndOpenAsync(
        IHostDriver driver,
        HostApp hostApp,
        string filePath,
        string? versionNumber,
        string? languageCode,
        CancellationToken cancellationToken)
    {
        HostLaunchResult launch;
        try
        {
            launch = await driver.LaunchAsync(
                new HostLaunchRequest(hostApp, versionNumber, languageCode, filePath), cancellationToken).ConfigureAwait(false);
        }
        catch (HostDriverException ex)
        {
            return ToolHelpers.ErrorResult(ex.Message);
        }

        var connected = await WaitForInstanceConnectionAsync(launch.ProcessId, cancellationToken).ConfigureAwait(false);
        var dialogResult = await HostLaunchCoordinator.TryAwaitResolverResultAsync(launch.DialogTask, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        if (!connected)
            return ToolHelpers.ErrorResult($"{hostApp} launched (PID={launch.ProcessId}) but bridge did not connect.");

        return new CallToolResult
        {
            Content = [new TextContentBlock
            {
                Text = JsonSerializer.Serialize(new OpenModelResult(
                    hostApp,
                    launch.ProcessId,
                    launch.Version,
                    launch.LanguageCode,
                    filePath,
                    true,
                    dialogResult))
            }]
        };
    }

    private static async Task<CallToolResult> OpenViaConnectedInstanceAsync(
        IHostMcpSession session, string filePath, CancellationToken cancellationToken)
    {
        try
        {
            return await session.CallToolAsync("open_document", new Dictionary<string, object?>
            {
                ["filePath"] = filePath
            }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ToolHelpers.ErrorResult("open_document request was canceled.");
        }
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

    private IHostMcpSession? ResolveSession(int? hostId)
    {
        if (hostId is > 0)
            return instanceManager.GetSessionByProcessId(hostId.Value);

        return instanceManager.Sessions.Count == 1 ? instanceManager.Sessions.Single() : null;
    }
}
