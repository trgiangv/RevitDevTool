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
public sealed class LaunchHostTool(HostSessionManager instanceManager, HostDriverRegistry drivers) : McpServerTool
{
    private static readonly string[] HostAppEnumNames =
        Enum.GetValues<HostApp>().Select(h => h.ToString()).ToArray();

    public override Tool ProtocolTool { get; } = new()
    {
        Name = "launch_host",
        Description =
            "Launch a host application and wait for the DevTools bridge to connect. " +
            "This is a long-running operation (typically 30-120 seconds for cold start). " +
            "Revit: version auto-detected from filePath when provided, otherwise latest installed. " +
            "AutoCAD family: always uses latest installed unless versionNumber is specified.",
        InputSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                hostApp = new { type = "string", description = "Revit, AutoCad, Civil3D, Plant3D, AcadArch, AcadMech, AcadElec, AcadMep, AcadMap3D, Navisworks" },
                versionNumber = new { type = "string", description = "Version year (e.g. '2025'). Revit auto-detects from filePath; AutoCAD defaults to latest." },
                languageCode = new { type = "string", description = "Revit-only: UI language code (default 'ENU')." },
                filePath = new { type = "string", description = "Model file to open at startup." }
            },
            required = new[] { "hostApp" }
        }),
        Execution = new ToolExecution { TaskSupport = ToolTaskSupport.Optional }
    };

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var args = request.Params.Arguments;
        var hostApp = HostAppExtensions.ParseHostApp(ReadString(args, "hostApp"));
        var version = ReadString(args, "versionNumber");
        var languageCode = ReadString(args, "languageCode");
        var filePath = ReadString(args, "filePath");

        if (hostApp is null)
            return ToolHelpers.ErrorResult(
                $"Invalid hostApp. Supported values: {string.Join(", ", HostAppEnumNames)}");

        if (!string.IsNullOrWhiteSpace(filePath) && !File.Exists(filePath))
            return ToolHelpers.ErrorResult($"File not found: {filePath}");

        var driver = drivers.TryForHost(hostApp.Value);
        if (driver is null)
            return ToolHelpers.ErrorResult($"Launch not yet supported for {hostApp}.");

        HostLaunchResult launch;
        try
        {
            launch = await driver.LaunchAsync(
                new HostLaunchRequest(hostApp.Value, version, languageCode, filePath), cancellationToken).ConfigureAwait(false);
        }
        catch (HostDriverException ex)
        {
            return ToolHelpers.ErrorResult(ex.Message);
        }

        var connected = await WaitForInstanceConnectionAsync(launch.ProcessId, cancellationToken).ConfigureAwait(false);
        var dialogResult = await HostLaunchCoordinator.TryAwaitResolverResultAsync(launch.DialogTask, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        if (!connected)
            return ToolHelpers.ErrorResult(
                $"{hostApp} launched (PID={launch.ProcessId}) but bridge did not connect within timeout.");

        var payload = new LaunchHostResult(
            hostApp.Value,
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

    private static string? ReadString(IDictionary<string, JsonElement>? args, string key) =>
        args is not null && args.TryGetValue(key, out var element) ? element.GetString() : null;
}
