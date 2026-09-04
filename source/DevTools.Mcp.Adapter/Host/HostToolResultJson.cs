using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Adapter.Bridging;
using DevTools.Mcp.Core.Invocation;
using DevTools.Mcp.Core.Protocol;

namespace DevTools.Mcp.Adapter.Host;

/// <summary>Host <c>tools/call</c> result JSON, including MRTR pass-through.</summary>
internal static class HostToolResultJson
{
    public static JsonNode ToNode(McpInvocationResponse response)
    {
        if (ToolsetMrtrBridge.TryGetInputRequiredResult(response, out var inputRequired) && inputRequired is not null)
        {
            return JsonSerializer.SerializeToNode(inputRequired, ToolHelpers.ProtocolOptions)
                   ?? new JsonObject();
        }

        return JsonSerializer.SerializeToNode(
                   SdkInvocationMapper.ToSdk(InvocationResponseEncoder.PrepareForWire(response)),
                   ToolHelpers.ProtocolOptions)
               ?? new JsonObject();
    }
}
