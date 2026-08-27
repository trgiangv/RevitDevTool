using System.ComponentModel;
using System.Runtime.Versioning;
using DevTools.Hosting;
using DevTools.Mcp.Client;
using DevTools.Mcp.Core;
using DevTools.Mcp.Core.Utils;
using DevTools.Mcp.Server.Contracts;
using DevTools.Mcp.Server.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Server.Tools;

[SupportedOSPlatform("windows")]
public sealed class LaunchHostTool(IHostBroker hostBroker, IHostLaunchService launchService)
{
    private static readonly TimeSpan BridgeTimeout = TimeSpan.FromMinutes(2);
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
                    "languageCode is a .NET culture name such as en-US (default en-US). " +
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
        [Description("UI language as a .NET culture name (default 'en-US').")]
        string? languageCode = null,
        [Description("Model file to open at startup. When set without hostApp, host is inferred from the extension.")]
        string? filePath = null,
        CancellationToken cancellationToken = default)
    {
        var parsedHost = HostAppParsing.ParseHostApp(hostApp);
        if (parsedHost is null && !string.IsNullOrWhiteSpace(filePath))
            parsedHost = HostAppExtensions.FromExtension(Path.GetExtension(filePath));

        if (parsedHost is null)
            return ToolHelpers.ErrorResult(
                $"hostApp is required (or provide filePath with a known extension). Supported values: {string.Join(", ", HostAppEnumNames)}");

        IReadOnlyDictionary<string, string>? options = string.IsNullOrWhiteSpace(languageCode)
            ? null
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [HostLaunchRequest.LanguageOptionKey] = languageCode.Trim()
            };

        var request = new HostLaunchRequest(
            parsedHost.Value,
            versionNumber ?? "",
            filePath,
            options);

        HostProcessStart started;
        try
        {
            started = launchService.Start(request, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return ToolHelpers.ErrorResult(ex.Message);
        }

        try
        {
            var status = await HostLaunchWaiter.UntilAsync(
                    started.Process,
                    () => hostBroker.GetByProcessId(started.Process.Id) is not null,
                    BridgeTimeout,
                    cancellationToken)
                .ConfigureAwait(false);

            var dialogResult = started.DialogResolver is null
                ? null
                : await started.DialogResolver.TryGetResultAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            if (status is not HostStatus.Ready)
            {
                var reason = status switch
                {
                    HostStatus.Exited => $"{parsedHost} launched (PID={started.Process.Id}) but exited before the bridge connected.",
                    HostStatus.Cancelled => $"{parsedHost} launch was cancelled (PID={started.Process.Id}).",
                    _ => $"{parsedHost} launched (PID={started.Process.Id}) but bridge did not connect within timeout."
                };
                return ToolHelpers.ErrorResult(reason);
            }

            var payload = new LaunchHostResult(
                parsedHost.Value,
                started.Process.Id,
                started.Version,
                started.ExePath,
                string.Join(" ", started.Arguments),
                started.LanguageCulture,
                true,
                dialogResult);

            return ToolHelpers.Result(payload);
        }
        finally
        {
            started.DialogResolver?.Dispose();
        }
    }
}
