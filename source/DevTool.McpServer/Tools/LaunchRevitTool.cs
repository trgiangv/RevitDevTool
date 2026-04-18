using System.Runtime.Versioning;
using System.Text.Json;
using DevTool.McpServer.Tools.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTool.McpServer.Tools;

[SupportedOSPlatform("windows")]
public sealed class LaunchRevitTool(InstanceManager instanceManager) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "launch_revit",
        Description = "Launch Autodesk Revit with safe defaults for automation. Always uses /nosplash, defaults language to ENU, and can optionally open a model file immediately.",
        InputSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                versionNumber = new { type = "string", description = "Revit version year to launch (e.g., '2025', '2024')." },
                languageCode = new { type = "string", description = "Optional Revit UI language code. Defaults to 'ENU'." },
                filePath = new { type = "string", description = "Optional absolute path to model/template/family file opened at startup." }
            },
            required = new[] { "versionNumber" }
        })
    };

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var args = request.Params?.Arguments;
        var version = ReadString(args, "versionNumber");
        var languageCode = ReadString(args, "languageCode");
        var filePath = ReadString(args, "filePath");

        var resolved = RevitLaunchCoordinator.Resolve(
            version,
            languageCode,
            filePath,
            requireVersion: true);
        if (resolved.Error is not null)
            return resolved.Error;

        var started = RevitLaunchCoordinator.StartProcess(resolved.Context!);
        if (started.Error is not null)
            return started.Error;

        RevitLaunchCoordinator.StartDialogResolver(started.Process!.Id, cancellationToken);
        var connected = await WaitForInstanceConnectionAsync(
            instanceManager,
            started.Process.Id,
            cancellationToken).ConfigureAwait(false);
        if (!connected)
            return ToolHelpers.ErrorResult($"Revit launched (PID={started.Process.Id}) but bridge did not connect.");

        var payload = new
        {
            processId = started.Process.Id,
            version = resolved.Context!.Version,
            path = resolved.Context.RevitPath,
            arguments = string.Join(" ", resolved.Context.Arguments),
            languageCode = resolved.Context.LanguageCode,
            bridgeConnected = true
        };
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(payload) }]
        };
    }

    private static string? ReadString(IDictionary<string, JsonElement>? args, string key)
    {
        return args is not null && args.TryGetValue(key, out var element)
            ? element.GetString()
            : null;
    }

    private static async Task<bool> WaitForInstanceConnectionAsync(
        InstanceManager instanceManager,
        int processId,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (instanceManager.GetByProcessId(processId) is not null)
                return true;

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return false;
    }

}
