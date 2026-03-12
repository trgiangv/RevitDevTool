using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.Contracts;

namespace RevitDevTool.Server;

public static class RevitToolAdapter
{
    public static McpServerTool ToMcpServerTool(Tool tool, string toolId, RevitBridgeClient bridgeClient)
    {
        return new BridgedMcpServerTool(tool, toolId, bridgeClient);
    }
}

internal sealed class BridgedMcpServerTool : McpServerTool
{
    private readonly string _toolId;
    private readonly RevitBridgeClient _bridgeClient;

    public BridgedMcpServerTool(Tool tool, string toolId, RevitBridgeClient bridgeClient)
    {
        _toolId = toolId;
        _bridgeClient = bridgeClient;
        ProtocolTool = tool;
    }

    public override Tool ProtocolTool { get; }

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var arguments = request.Params?.Arguments;
        var payloadJson = arguments is not null
            ? JsonSerializer.Serialize(arguments, McpJsonUtilities.DefaultOptions)
            : "{}";

        var result = await _bridgeClient.CallToolAsync(
            _toolId,
            ProtocolTool.Name,
            payloadJson,
            cancellationToken).ConfigureAwait(false);

        if (result.State == ExecutionState.Completed)
            return result.Result;

        var errorMsg = result.Error?.Message ?? result.Detail;
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = errorMsg }]
        };
    }
}
