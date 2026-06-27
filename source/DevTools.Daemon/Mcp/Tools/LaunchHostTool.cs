using System.Runtime.Versioning;
using System.Text.Json;
using DevTools.Logging;
using DevTools.Daemon.Mcp.Tools.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

[SupportedOSPlatform("windows")]
public sealed class LaunchHostTool(InstanceManager instanceManager) : McpServerTool
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
            type = JsonSchemaTypeNames.Object,
            properties = new
            {
                hostApp = new
                {
                    type = JsonSchemaTypeNames.String,
                    description = "Revit, AutoCad, Civil3D, Plant3D, AcadArch, AcadMech, AcadElec, AcadMep, AcadMap3D, Navisworks"
                },
                versionNumber = new
                {
                    type = JsonSchemaTypeNames.String,
                    description = "Version year (e.g. '2025'). Revit auto-detects from filePath; AutoCAD defaults to latest."
                },
                languageCode = new
                {
                    type = JsonSchemaTypeNames.String,
                    description = "Revit-only: UI language code (default 'ENU')."
                },
                filePath = new
                {
                    type = JsonSchemaTypeNames.String,
                    description = "Model file to open at startup."
                }
            },
            required = new[] { McpPropertyNames.HostApp }
        }),
        Execution = new ToolExecution { TaskSupport = ToolTaskSupport.Optional }
    };

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var args = request.Params.Arguments;
        var hostApp = HostAppExtensions.ParseHostApp(ReadString(args, McpPropertyNames.HostApp));
        var version = ReadString(args, McpPropertyNames.VersionNumber);
        var languageCode = ReadString(args, McpPropertyNames.LanguageCode);
        var filePath = ReadString(args, McpPropertyNames.FilePath);

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
        HostLaunchCoordinator.StartDialogResolver(hostApp.Value, process.Id, cancellationToken);

        var connected = await WaitForInstanceConnectionAsync(process.Id, cancellationToken).ConfigureAwait(false);
        if (!connected)
            return ToolHelpers.ErrorResult(
                $"{hostApp} launched (PID={process.Id}) but bridge did not connect within timeout.");

        var payload = new
        {
            hostApp = hostApp.Value.ToString(),
            processId = process.Id,
            version = context.Version,
            path = context.ExePath,
            arguments = string.Join(" ", context.Arguments),
            languageCode = context.LanguageCode,
            bridgeConnected = true
        };

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
            if (instanceManager.GetByProcessId(processId) is not null)
                return true;

            try { await Task.Delay(500, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return false; }
        }

        return false;
    }

    private static string? ReadString(IDictionary<string, JsonElement>? args, string key) =>
        args is not null && args.TryGetValue(key, out var element) ? element.GetString() : null;
}
