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

public sealed class ReadDynamicResource(InstanceManager instanceManager, DynamicResourceCatalog catalog) : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "read_dynamic_resource",
        Description =
            "Read a resource by URI from a connected host instance. " +
            "Specify hostInstanceId when multiple instances provide the same resource.",
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

        var hostInstanceId = ReadInt32(args, McpPropertyNames.HostInstanceId);
        var resolution = catalog.Resolve(uri!, hostInstanceId);

        switch (resolution.State)
        {
            case DynamicResolutionState.NotFound:
                return ToolHelpers.ErrorResult(hostInstanceId is null
                    ? $"Resource '{uri}' is not registered by any connected instance."
                    : $"Resource '{uri}' is not registered by host instance {hostInstanceId}.");
            case DynamicResolutionState.Ambiguous:
                return ToolHelpers.ErrorResult(
                    $"Resource '{uri}' is available on multiple instances. Specify hostInstanceId: " +
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

        var contentBlocks = new List<ContentBlock>();
        foreach (var content in readResult.Contents)
        {
            switch (content)
            {
                case TextResourceContents text when !string.IsNullOrEmpty(text.Text):
                    contentBlocks.Add(new TextContentBlock { Text = text.Text });
                    break;
                case BlobResourceContents blob:
                    contentBlocks.Add(ImageContentBlock.FromBytes(
                        blob.DecodedData,
                        blob.MimeType ?? "application/octet-stream"));
                    break;
            }
        }

        return new CallToolResult
        {
            Content = contentBlocks.Count > 0
                ? contentBlocks
                : [new TextContentBlock { Text = "(empty resource)" }]
        };
    }

    private static string FormatInstance(DynamicResourceCatalogEntry item) =>
        $"PID {item.Instance.ProcessId} ({item.Instance.HostApp ?? "unknown"} {item.Instance.VersionNumber})";

    private static int? ReadInt32(IDictionary<string, JsonElement>? args, string key) =>
        args is not null && args.TryGetValue(key, out var value) && value.TryGetInt32(out var result) ? result : null;
}
