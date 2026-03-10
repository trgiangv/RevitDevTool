using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.Contracts;

namespace RevitDevTool.Server;

public static class RevitToolAdapter
{
    public static McpServerTool ToMcpServerTool(McpToolDefinition definition, RevitBridgeClient bridgeClient)
    {
        return new BridgedMcpServerTool(definition, bridgeClient);
    }
}

internal sealed class BridgedMcpServerTool : McpServerTool
{
    private readonly McpToolDefinition _definition;
    private readonly RevitBridgeClient _bridgeClient;

    public BridgedMcpServerTool(McpToolDefinition definition, RevitBridgeClient bridgeClient)
    {
        _definition = definition;
        _bridgeClient = bridgeClient;

        JsonElement? inputSchema = null;
        if (!string.IsNullOrWhiteSpace(definition.InputSchemaJson))
        {
            try
            {
                inputSchema = JsonSerializer.Deserialize<JsonElement>(definition.InputSchemaJson);
            }
            catch
            {
                // Fall back to empty object schema
            }
        }

        ProtocolTool = new Tool
        {
            Name = definition.Name,
            Description = definition.Description,
            InputSchema = inputSchema ?? JsonSerializer.Deserialize<JsonElement>("""{"type":"object","properties":{}}"""),
            Annotations = BuildAnnotations(definition),
        };
    }

    public override Tool ProtocolTool { get; }

    public override IReadOnlyList<object> Metadata => [];

    private static ToolAnnotations? BuildAnnotations(McpToolDefinition definition)
    {
        var annotations = definition.Annotations;
        var preferredTitle = !string.IsNullOrWhiteSpace(definition.DisplayName) && definition.DisplayName != definition.Name
            ? definition.DisplayName
            : null;
        
        if (preferredTitle is null)
            return HasAnyAnnotation(annotations) ? annotations : null;
        
        return new ToolAnnotations
        {
            Title = preferredTitle,
            ReadOnlyHint = annotations?.ReadOnlyHint,
            DestructiveHint = annotations?.DestructiveHint,
            IdempotentHint = annotations?.IdempotentHint,
            OpenWorldHint = annotations?.OpenWorldHint,
        };
    }

    private static bool HasAnyAnnotation(ToolAnnotations? annotations)
        => !string.IsNullOrWhiteSpace(annotations?.Title)
            || annotations?.ReadOnlyHint is not null
            || annotations?.DestructiveHint is not null
            || annotations?.IdempotentHint is not null
            || annotations?.OpenWorldHint is not null;

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var arguments = request.Params?.Arguments;
        var payloadJson = arguments is not null
            ? JsonSerializer.Serialize(arguments)
            : "{}";

        var result = await _bridgeClient.CallToolAsync(
            _definition.ToolId,
            _definition.Name,
            payloadJson,
            cancellationToken).ConfigureAwait(false);

        if (result.Success)
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = result.PayloadJson }]
            };
        var errorMsg = result.Error?.Message ?? result.Message;
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = errorMsg }]
        };

    }
}
