using System.Text.Json;
using DevTools.Ipc;
using DevTools.Mcp;
using DevTools.Mcp.Models;
using DevTools.Mcp.Routing;
using DevTools.Mcp.Routing.Catalog;
using DevTools.Mcp.Schema;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

public sealed class GetDynamicPrompt(HostSessionManager instanceManager, DynamicPromptCatalog catalog) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "get_dynamic_prompt",
        Description =
            "Get a prompt by name from a connected host instance. " +
            "Specify hostInstanceId when multiple instances provide the same prompt.",
        InputSchema = McpSchemaBuilder.Object(
        [
            McpSchemaBuilder.String(IpcPropertyNames.Name, "Prompt name."),
            McpSchemaBuilder.ObjectProp(IpcPropertyNames.Arguments, "Prompt arguments (if required by the prompt)."),
            McpSchemaBuilder.Integer(McpPropertyNames.HostInstanceId, "Target host process ID (optional).")
        ],
        required: [IpcPropertyNames.Name])
    };

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var args = request.Params.Arguments;
        if (args is null || !args.TryGetValue(IpcPropertyNames.Name, out var nameElement))
            return ToolHelpers.ErrorResult("Missing required 'name' parameter.");

        var name = nameElement.GetString();
        if (string.IsNullOrWhiteSpace(name))
            return ToolHelpers.ErrorResult("Prompt name must not be empty.");

        var hostInstanceId = ReadInt32(args, McpPropertyNames.HostInstanceId);
        var resolution = catalog.Resolve(name!, hostInstanceId);

        switch (resolution.State)
        {
            case DynamicResolutionState.NotFound:
                return ToolHelpers.ErrorResult(hostInstanceId is null
                    ? $"Prompt '{name}' is not registered by any connected instance."
                    : $"Prompt '{name}' is not registered by host instance {hostInstanceId}.");
            case DynamicResolutionState.Ambiguous:
                return ToolHelpers.ErrorResult(
                    $"Prompt '{name}' is available on multiple instances. Specify hostInstanceId: " +
                    string.Join(", ", resolution.Candidates.Select(FormatInstance)));
            case DynamicResolutionState.Found:
                break;
            default:
                throw new ArgumentOutOfRangeException($"Unexpected resolution state: {resolution.State}");
        }

        var registration = resolution.Registration!;
        var client = instanceManager.GetByProcessId(registration.Instance.ProcessId);
        if (client is null || !client.IsConnected)
            return ToolHelpers.ErrorResult($"Host instance {registration.Instance.ProcessId} is no longer connected.");

        Dictionary<string, JsonElement>? promptArgs = null;
        if (args.TryGetValue(IpcPropertyNames.Arguments, out var argsElement) &&
            argsElement.ValueKind == JsonValueKind.Object)
        {
            promptArgs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argsElement.GetRawText());
        }

        var callParams = JsonSerializer.SerializeToElement(new McpPromptsGetParams
        {
            Name = name!,
            Arguments = promptArgs
        });

        var response = await client.RequestAsync(McpBridgeMethods.PromptsGet, callParams, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsError)
            return ToolHelpers.ErrorResult(response.ErrorMessage ?? "Prompt call failed.");

        if (response.Result is not { } result)
            return ToolHelpers.ErrorResult("No result returned.");

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.GetRawText() }]
        };
    }

    private static string FormatInstance(DynamicPromptCatalogEntry item) =>
        $"PID {item.Instance.ProcessId} ({item.Instance.HostApp ?? "unknown"} {item.Instance.VersionNumber})";

    private static int? ReadInt32(IDictionary<string, JsonElement>? args, string key) =>
        args is not null && args.TryGetValue(key, out var value) && value.TryGetInt32(out var result) ? result : null;
}
