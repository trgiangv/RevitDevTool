using System.Runtime.Versioning;
using System.Text.Json;
using DevTools.Daemon.Contracts;
using DevTools.Logging;
using DevTools.Daemon.Mcp.Tools.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

[SupportedOSPlatform("windows")]
public sealed class LaunchHostTool(HostSessionManager instanceManager) : McpServerTool
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

        var resolved = HostLaunchCoordinator.Resolve(
            hostApp.Value, version, languageCode, filePath, requireVersion: false);
        if (resolved.Error is not null)
            return resolved.Error;

        var context = resolved.Context!;
        var started = HostLaunchCoordinator.StartProcess(context);
        if (started.Error is not null)
            return started.Error;

        var process = started.Process!;
        var dialogTask = HostLaunchCoordinator.StartDialogResolver(hostApp.Value, process.Id, cancellationToken);

        var connected = await WaitForInstanceConnectionAsync(process.Id, cancellationToken).ConfigureAwait(false);
        var dialogResult = await HostLaunchCoordinator.TryAwaitResolverResultAsync(dialogTask, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        if (!connected)
            return ToolHelpers.ErrorResult(
                $"{hostApp} launched (PID={process.Id}) but bridge did not connect within timeout.");

        var payload = new LaunchHostResult(
            hostApp.Value,
            process.Id,
            context.Version,
            context.ExePath,
            string.Join(" ", context.Arguments),
            context.LanguageCode,
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
