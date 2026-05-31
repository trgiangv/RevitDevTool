using System.Text.Json;
using DevTools.Logging;
using DevTools.McpParser.Models;
using DevTools.McpServer.Tools.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.McpServer.Tools;

public sealed class OpenModelTool(InstanceManager instanceManager) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "open_model",
        Description =
            "Open a model file in a connected host or launch a new one. " +
            "Host is auto-detected from extension: .rvt/.rfa → Revit, .dwg/.dxf/.dwt → AutoCAD.",
        InputSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                filePath = new
                {
                    type = "string",
                    description = "Full path to the model file."
                },
                hostInstanceId = new
                {
                    type = "integer",
                    description = "Target host process ID (when multiple instances exist)."
                },
                versionNumber = new
                {
                    type = "string",
                    description = "Version to launch if no instance is connected."
                },
                languageCode = new
                {
                    type = "string",
                    description = "Revit-only: UI language code (default 'ENU')."
                }
            },
            required = new[] { "filePath" }
        })
    };

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var args = request.Params.Arguments ?? new Dictionary<string, JsonElement>();

        var filePath = ReadString(args, "filePath");
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
        var versionNumber = ReadString(args, "versionNumber");
        var languageCode = ReadString(args, "languageCode");

        var resolved = HostLaunchCoordinator.Resolve(
            hostApp, versionNumber, languageCode, filePath, requireVersion: false);
        if (resolved.Error is not null)
            return resolved.Error;

        var context = resolved.Context!;
        var started = HostLaunchCoordinator.StartProcess(context);
        if (started.Error is not null)
            return started.Error;

        var process = started.Process!;
        HostLaunchCoordinator.StartDialogResolver(hostApp, process.Id, cancellationToken);

        var connected = await WaitForInstanceConnectionAsync(process.Id, cancellationToken).ConfigureAwait(false);
        if (!connected)
            return ToolHelpers.ErrorResult($"{hostApp} launched (PID={process.Id}) but bridge did not connect.");

        return new CallToolResult
        {
            Content = [new TextContentBlock
            {
                Text = JsonSerializer.Serialize(new
                {
                    hostApp = hostApp.ToString(),
                    processId = process.Id,
                    version = context.Version,
                    languageCode = context.LanguageCode,
                    filePath,
                    bridgeConnected = true
                })
            }]
        };
    }

    private static async Task<CallToolResult> OpenViaConnectedInstanceAsync(
        HostBridgeClient client, string filePath, CancellationToken cancellationToken)
    {
        var callParams = JsonSerializer.SerializeToElement(new
        {
            name = "open_document",
            arguments = new { file_path = filePath }
        });

        try
        {
            var response = await client.RequestAsync(BridgeMethods.ToolsCall, callParams, cancellationToken)
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
