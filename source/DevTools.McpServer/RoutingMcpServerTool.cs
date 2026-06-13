using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using DevTools.McpParser.Models;

namespace DevTools.McpServer;

public sealed class RoutingMcpServerTool(InstanceManager instanceManager, Tool tool) : McpServerTool
{
    public override Tool ProtocolTool { get; } = InjectInstanceIdParam(tool);

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var args = request.Params.Arguments ?? new Dictionary<string, JsonElement>();

        var client = ToolHelpers.ResolveClient(instanceManager, args, out var cleanedArgs);
        if (client is null)
            return ToolHelpers.ErrorResult(ToolHelpers.FormatInstanceListing(instanceManager));

        var callParamsObj = new Dictionary<string, object?> { [McpPropertyNames.Name] = ProtocolTool.Name };
        if (cleanedArgs.Count > 0)
            callParamsObj[McpPropertyNames.Arguments] = cleanedArgs;

        var callParams = JsonSerializer.SerializeToElement(callParamsObj);
        var response = await client.RequestAsync(BridgeMethods.ToolsCall, callParams, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsError)
            return ToolHelpers.ErrorResult(response.ErrorMessage ?? "Tool call failed.");

        if (response.Result is { } result)
            return JsonSerializer.Deserialize<CallToolResult>(result.GetRawText()) ?? ToolHelpers.ErrorResult("Empty result.");

        return ToolHelpers.ErrorResult("No result returned.");
    }

    private static Tool InjectInstanceIdParam(Tool original)
    {
        var instanceIdSchema = JsonSerializer.SerializeToElement(new
        {
            type = JsonSchemaTypeNames.Integer,
            description = "Target host process ID (use list_host_instances to find available instances)."
        });

        var existingSchema = original.InputSchema;
        var schemaDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(existingSchema.GetRawText())
                         ?? new Dictionary<string, JsonElement>();

        var propsDict = new Dictionary<string, JsonElement>();
        if (schemaDict.TryGetValue(McpPropertyNames.Properties, out var propsElement) &&
            propsElement.ValueKind == JsonValueKind.Object)
        {
            propsDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(propsElement.GetRawText())
                        ?? new Dictionary<string, JsonElement>();
        }

        propsDict[McpPropertyNames.HostInstanceId] = instanceIdSchema;
        schemaDict[McpPropertyNames.Properties] = JsonSerializer.SerializeToElement(propsDict);

        return new Tool
        {
            Name = original.Name,
            Description = original.Description,
            InputSchema = JsonSerializer.SerializeToElement(schemaDict),
            Annotations = original.Annotations
        };
    }
}
