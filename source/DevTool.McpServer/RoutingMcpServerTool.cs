using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using DevTool.McpParser.Models;

namespace DevTool.McpServer;

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

        var callParamsObj = new Dictionary<string, object?> { ["name"] = ProtocolTool.Name };
        if (cleanedArgs.Count > 0)
            callParamsObj["arguments"] = cleanedArgs;

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
            type = "integer",
            description = "Target Revit process ID. Required when multiple instances are connected. Use list_revit_instances to discover."
        });

        var existingSchema = original.InputSchema;
        var schemaDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(existingSchema.GetRawText())
                         ?? new Dictionary<string, JsonElement>();

        var propsDict = new Dictionary<string, JsonElement>();
        if (schemaDict.TryGetValue("properties", out var propsElement) &&
            propsElement.ValueKind == JsonValueKind.Object)
        {
            propsDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(propsElement.GetRawText())
                        ?? new Dictionary<string, JsonElement>();
        }

        propsDict["revitInstanceId"] = instanceIdSchema;
        schemaDict["properties"] = JsonSerializer.SerializeToElement(propsDict);

        return new Tool
        {
            Name = original.Name,
            Description = original.Description,
            InputSchema = JsonSerializer.SerializeToElement(schemaDict),
            Annotations = original.Annotations
        };
    }
}
