using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.McpParser.Models;
using RevitDevTool.McpServer.Tools.Utils;

namespace RevitDevTool.McpServer.Tools;

public sealed class OpenRevitModelTool(InstanceManager instanceManager) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "open_revit_model",
        Description = "Open a Revit model with robust behavior: if a Revit instance is connected, route to its open_document tool; otherwise launch a new Revit instance with /nosplash, default ENU language, and the target model path.",
        InputSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                filePath = new { type = "string", description = "Full path to the Revit file to open" },
                revitInstanceId = new { type = "integer", description = "Target Revit process ID. Required when multiple instances are connected." },
                versionNumber = new { type = "string", description = "Version to launch when no instance is connected. Optional; defaults to latest installed version." },
                languageCode = new { type = "string", description = "Optional launch language code when spawning a new instance. Defaults to ENU." }
            },
            required = new[] { "filePath" }
        })
    };

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var args = request.Params?.Arguments ?? new Dictionary<string, JsonElement>();
        var parsed = ParseRequest(args);
        if (parsed.Error is not null)
            return parsed.Error;

        var model = parsed.Request!;
        var client = ToolHelpers.ResolveClient(instanceManager, args, out _);
        if (client is not null)
            return await OpenViaConnectedInstanceAsync(client, model.FilePath, cancellationToken).ConfigureAwait(false);

        return await LaunchAndOpenAsync(model, cancellationToken).ConfigureAwait(false);
    }

    private static (OpenModelRequest? Request, CallToolResult? Error) ParseRequest(IDictionary<string, JsonElement> args)
    {
        var filePath = ReadString(args, "filePath");
        if (string.IsNullOrWhiteSpace(filePath))
            return (null, ToolHelpers.ErrorResult("filePath is required."));
        if (!File.Exists(filePath))
            return (null, ToolHelpers.ErrorResult($"File not found: {filePath}"));

        var request = new OpenModelRequest(
            filePath,
            ReadString(args, "versionNumber"),
            ReadString(args, "languageCode"));
        return (request, null);
    }

    private async Task<CallToolResult> LaunchAndOpenAsync(OpenModelRequest request, CancellationToken cancellationToken)
    {
        var launch = RevitLaunchCoordinator.Resolve(
            request.VersionNumber,
            request.LanguageCode,
            request.FilePath,
            requireVersion: false);
        if (launch.Error is not null)
            return launch.Error;

        var context = launch.Context!;
        var started = RevitLaunchCoordinator.StartProcess(context);
        if (started.Error is not null)
            return started.Error;

        var process = started.Process!;
        RevitLaunchCoordinator.StartDialogResolver(process.Id, cancellationToken);

        var connected = await WaitForInstanceConnectionAsync(
            instanceManager,
            process.Id,
            cancellationToken).ConfigureAwait(false);
        if (!connected)
            return ToolHelpers.ErrorResult($"Revit launched (PID={process.Id}) but bridge did not connect.");

        return new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = JsonSerializer.Serialize(new
                    {
                        processId = process.Id,
                        version = context.Version,
                        languageCode = context.LanguageCode,
                        filePath = request.FilePath,
                        arguments = string.Join(" ", context.Arguments),
                        bridgeConnected = true
                    })
                }
            ]
        };
    }

    private static async Task<CallToolResult> OpenViaConnectedInstanceAsync(
        RevitBridgeClient client,
        string filePath,
        CancellationToken cancellationToken)
    {
        var callParams = BuildOpenDocumentCallParams(filePath);
        try
        {
            var response = await RequestToolCallAsync(client, callParams, cancellationToken).ConfigureAwait(false);
            return MapOpenDocumentResponse(response, filePath);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ToolHelpers.ErrorResult("open_document request was canceled.");
        }
    }

    private static JsonElement BuildOpenDocumentCallParams(string filePath)
    {
        return JsonSerializer.SerializeToElement(new
        {
            name = "open_document",
            arguments = new
            {
                file_path = filePath
            }
        });
    }

    private static Task<BridgeMessage> RequestToolCallAsync(
        RevitBridgeClient client,
        JsonElement callParams,
        CancellationToken cancellationToken)
    {
        return client.RequestAsync(BridgeMethods.ToolsCall, callParams, cancellationToken);
    }

    private static CallToolResult MapOpenDocumentResponse(BridgeMessage response, string filePath)
    {
        if (response.IsError)
            return ToolHelpers.ErrorResult(response.ErrorMessage ?? "Failed to open model in connected instance.");

        if (response.Result is not { } result)
            return SuccessFallback(filePath);

        var callResult = JsonSerializer.Deserialize<CallToolResult>(result.GetRawText());
        return callResult ?? ToolHelpers.ErrorResult("Empty result.");
    }

    private static CallToolResult SuccessFallback(string filePath)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = $"Model '{Path.GetFileName(filePath)}' opened successfully." }]
        };
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

    private sealed record OpenModelRequest(
        string FilePath,
        string? VersionNumber,
        string? LanguageCode);

    private static string? ReadString(IDictionary<string, JsonElement>? args, string key)
    {
        return args is not null && args.TryGetValue(key, out var element)
            ? element.GetString()
            : null;
    }

}
