using System.Text.Json;
using DevTools.Daemon.Contracts;
using DevTools.Logging;
using DevTools.Daemon.Mcp.Tools.Utils;
using DevTools.Mcp.Schema;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

/// <summary>
/// Opens a model file in a host application. Two strategies:
/// 1. Connected instance: routes <c>open_document</c> built-in tool via Named Pipe.
/// 2. No instance: launches the host process with the file as a CLI argument.
/// </summary>
public sealed class OpenModelTool(HostSessionManager instanceManager) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "open_model",
        Description =
            "Open a model file in a connected host or launch a new one. " +
            "When launching a new host, this is a long-running operation (typically 30-120 seconds). " +
            "Host is auto-detected from extension: .rvt/.rfa → Revit, .dwg/.dxf/.dwt → AutoCAD.",
        InputSchema = McpSchemaBuilder.Object(
        [
            McpSchemaBuilder.String(McpPropertyNames.FilePath, "Full path to the model file."),
            McpSchemaBuilder.Integer(
                McpPropertyNames.HostInstanceId,
                "Target host process ID (when multiple instances exist)."),
            McpSchemaBuilder.String(
                IpcPropertyNames.VersionNumber,
                "Version to launch if no instance is connected."),
            McpSchemaBuilder.String(
                McpPropertyNames.LanguageCode,
                "Revit-only: UI language code (default 'ENU').")
        ],
        required: [McpPropertyNames.FilePath]),
        Execution = new ToolExecution { TaskSupport = ToolTaskSupport.Optional }
    };

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var args = request.Params.Arguments ?? new Dictionary<string, JsonElement>();

        var filePath = ReadString(args, McpPropertyNames.FilePath);
        if (string.IsNullOrWhiteSpace(filePath))
            return ToolHelpers.ErrorResult("filePath is required.");
        if (!File.Exists(filePath))
            return ToolHelpers.ErrorResult($"File not found: {filePath}");

        var hostApp = HostAppExtensions.FromExtension(Path.GetExtension(filePath));
        if (hostApp is null)
            return ToolHelpers.ErrorResult($"Cannot determine host application from file extension '{Path.GetExtension(filePath)}'.");

        var client = ToolHelpers.ResolveClient(instanceManager, args, out _);
        if (client is not null)
            return await OpenViaConnectedInstanceAsync(client, filePath, cancellationToken).ConfigureAwait(false);

        return await LaunchAndOpenAsync(hostApp.Value, filePath, args, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CallToolResult> LaunchAndOpenAsync(
        HostApp hostApp,
        string filePath,
        IDictionary<string, JsonElement> args,
        CancellationToken cancellationToken)
    {
        var versionNumber = ReadString(args, IpcPropertyNames.VersionNumber);
        var languageCode = ReadString(args, McpPropertyNames.LanguageCode);

        var resolved = HostLaunchCoordinator.Resolve(
            hostApp, versionNumber, languageCode, filePath, requireVersion: false);
        if (resolved.Error is not null)
            return resolved.Error;

        var context = resolved.Context!;
        var started = HostLaunchCoordinator.StartProcess(context);
        if (started.Error is not null)
            return started.Error;

        var process = started.Process!;
        var dialogTask = HostLaunchCoordinator.StartDialogResolver(hostApp, process.Id, cancellationToken);

        var connected = await WaitForInstanceConnectionAsync(process.Id, cancellationToken).ConfigureAwait(false);
        var dialogResult = await HostLaunchCoordinator.TryAwaitResolverResultAsync(dialogTask, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        if (!connected)
            return ToolHelpers.ErrorResult($"{hostApp} launched (PID={process.Id}) but bridge did not connect.");

        return new CallToolResult
        {
            Content = [new TextContentBlock
            {
                Text = JsonSerializer.Serialize(new OpenModelResult(
                    hostApp,
                    process.Id,
                    context.Version,
                    context.LanguageCode,
                    filePath,
                    true,
                    dialogResult))
            }]
        };
    }

    private static async Task<CallToolResult> OpenViaConnectedInstanceAsync(
        IHostBridgeClient client, string filePath, CancellationToken cancellationToken)
    {
        var callParams = JsonSerializer.SerializeToElement(new McpToolsCallParams
        {
            Name = "open_document",
            Arguments = new Dictionary<string, JsonElement>
            {
                [McpPropertyNames.FilePath] = JsonSerializer.SerializeToElement(filePath)
            }
        });

        try
        {
            var response = await client.RequestAsync(McpBridgeMethods.ToolsCall, callParams, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsError)
                return ToolHelpers.ErrorResult(response.ErrorMessage ?? "Failed to open model in connected instance.");

            if (response.Result is not { } result)
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = $"Model '{Path.GetFileName(filePath)}' opened successfully." }]
                };

            return JsonSerializer.Deserialize<CallToolResult>(result.GetRawText())
                   ?? ToolHelpers.ErrorResult("Empty result.");
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
