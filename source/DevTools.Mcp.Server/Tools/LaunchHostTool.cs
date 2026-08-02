using System.ComponentModel;
using System.Runtime.Versioning;
using DevTools.Logging;
using DevTools.Mcp.Client;
using DevTools.Mcp.Core;
using DevTools.Mcp.Core.Utils;
using DevTools.Mcp.Server.Contracts;
using DevTools.Mcp.Server.Hosting;
using DevTools.Mcp.Server.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Server.Tools;

[SupportedOSPlatform("windows")]
public sealed class LaunchHostTool(IHostBroker hostBroker, IHostLaunchService launchService)
{
    private static readonly string[] HostAppEnumNames =
        Enum.GetValues<HostApp>().Select(h => h.ToString()).ToArray();

    public static McpServerTool Create(IHostBroker hostBroker, IHostLaunchService launchService)
    {
        var handler = new LaunchHostTool(hostBroker, launchService);
        return McpServerTool.Create(
            handler.LaunchAsync,
            new McpServerToolCreateOptions
            {
                Name = "launch_host",
                Description =
                    "Launch a host application (optionally opening a model file at startup) and wait for the DevTools bridge to connect. " +
                    "This is a long-running operation (typically 30-120 seconds for cold start). " +
                    "hostApp is required unless filePath is set — then the host is inferred from the extension " +
                    "(.rvt/.rfa → Revit, .dwg/.dxf/.dwt → AutoCAD). " +
                    "To open a file in an already-running host, use invoke_dynamic on open_document instead.",
                Destructive = true,
                OpenWorld = true
            });
    }

    [Description("Launch a host application and wait for the DevTools bridge to connect.")]
    public async Task<CallToolResult> LaunchAsync(
        [Description("Revit, AutoCad, Civil3D, Plant3D, AcadArch, AcadMech, AcadElec, AcadMep, AcadMap3D, Navisworks. Optional when filePath is set.")]
        string? hostApp = null,
        [Description("Version year (e.g. '2025'). Revit auto-detects from filePath; AutoCAD defaults to latest.")]
        string? versionNumber = null,
        [Description("Revit-only: UI language code (default 'ENU').")]
        string? languageCode = null,
        [Description("Model file to open at startup. When set without hostApp, host is inferred from the extension.")]
        string? filePath = null,
        CancellationToken cancellationToken = default)
    {
        var parsedHost = HostAppExtensions.ParseHostApp(hostApp);
        if (parsedHost is null && !string.IsNullOrWhiteSpace(filePath))
            parsedHost = HostAppExtensions.FromExtension(Path.GetExtension(filePath));

        if (parsedHost is null)
            return ToolHelpers.ErrorResult(
                $"hostApp is required (or provide filePath with a known extension). Supported values: {string.Join(", ", HostAppEnumNames)}");

        HostProcessStart started;
        try
        {
            started = launchService.Start(
                parsedHost.Value, versionNumber, languageCode, filePath, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return ToolHelpers.ErrorResult(ex.Message);
        }

        var connected = await WaitForInstanceConnectionAsync(started.Process.Id, cancellationToken).ConfigureAwait(false);
        var dialogResult = await HostLaunchCoordinator.TryAwaitResolverResultAsync(
            started.DialogResolver, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        if (!connected)
            return ToolHelpers.ErrorResult(
                $"{parsedHost} launched (PID={started.Process.Id}) but bridge did not connect within timeout.");

        var payload = new LaunchHostResult(
            parsedHost.Value,
            started.Process.Id,
            started.Version,
            started.ExePath,
            string.Join(" ", started.Arguments),
            started.LanguageCode,
            true,
            dialogResult);

        return ToolHelpers.Result(payload);
    }

    private async Task<bool> WaitForInstanceConnectionAsync(int processId, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (hostBroker.GetByProcessId(processId) is not null)
                return true;

            try { await Task.Delay(500, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return false; }
        }

        return false;
    }
}
