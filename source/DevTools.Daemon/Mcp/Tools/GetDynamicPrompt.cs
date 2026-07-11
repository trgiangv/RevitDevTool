using System.Text.Json;
using DevTools.Ipc;
using DevTools.Mcp;
using DevTools.Mcp.Models;
using DevTools.Mcp.Routing;
using DevTools.Mcp.Schema;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

public sealed class GetDynamicPrompt(InstanceManager instanceManager) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "get_dynamic_prompt",
        Description =
            "Get a prompt by name from a connected host instance. " +
            "Use list_dynamic_prompts to discover available prompt names.",
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

        var client = ResolveClient(args);
        if (client is null)
            return ToolHelpers.ErrorResult(ToolHelpers.FormatInstanceListing(instanceManager));

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

    private IHostBridgeClient? ResolveClient(IDictionary<string, JsonElement> args)
    {
        if (args.TryGetValue(McpPropertyNames.HostInstanceId, out var pidElement))
            return instanceManager.GetByProcessId(ToolHelpers.ParseProcessId(pidElement));
        return ((IInstanceManager)instanceManager).GetDefault();
    }
}
