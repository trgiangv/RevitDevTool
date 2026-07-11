using System.Text.Json;
using DevTools.Ipc;
using DevTools.Mcp;
using DevTools.Mcp.Models;
using DevTools.Mcp.Routing;
using DevTools.Mcp.Schema;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

public sealed class ReadDynamicResource(InstanceManager instanceManager) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "read_dynamic_resource",
        Description =
            "Read a resource by URI from a connected host instance. " +
            "Use list_dynamic_resources to discover available URIs.",
        InputSchema = McpSchemaBuilder.Object(
        [
            McpSchemaBuilder.String("uri", "Resource URI to read (e.g. revit://model/context)."),
            McpSchemaBuilder.Integer(McpPropertyNames.HostInstanceId, "Target host process ID (optional).")
        ],
        required: ["uri"])
    };

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var args = request.Params.Arguments;
        if (args is null || !args.TryGetValue("uri", out var uriElement))
            return ToolHelpers.ErrorResult("Missing required 'uri' parameter.");

        var uri = uriElement.GetString();
        if (string.IsNullOrWhiteSpace(uri))
            return ToolHelpers.ErrorResult("URI must not be empty.");

        var client = ResolveClient(args);
        if (client is null)
            return ToolHelpers.ErrorResult(ToolHelpers.FormatInstanceListing(instanceManager));

        var callParams = JsonSerializer.SerializeToElement(new McpResourcesReadParams { Uri = uri! });
        var response = await client.RequestAsync(McpBridgeMethods.ResourcesRead, callParams, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsError)
            return ToolHelpers.ErrorResult(response.ErrorMessage ?? "Resource read failed.");

        if (response.Result is not { } result)
            return ToolHelpers.ErrorResult("No result returned.");

        var readResult = JsonSerializer.Deserialize<ReadResourceResult>(result.GetRawText());
        if (readResult is null)
            return ToolHelpers.ErrorResult("Empty resource result.");

        var texts = readResult.Contents
            .OfType<TextResourceContents>()
            .Select(c => c.Text)
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        return new CallToolResult
        {
            Content = texts.Count > 0
                ? texts.Select(t => (ContentBlock)new TextContentBlock { Text = t }).ToList()
                : [new TextContentBlock { Text = "(empty resource)" }]
        };
    }

    private IHostBridgeClient? ResolveClient(IDictionary<string, JsonElement> args)
    {
        if (args.TryGetValue(McpPropertyNames.HostInstanceId, out var pidElement))
            return instanceManager.GetByProcessId(ToolHelpers.ParseProcessId(pidElement));
        return ((IInstanceManager)instanceManager).GetDefault();
    }
}
