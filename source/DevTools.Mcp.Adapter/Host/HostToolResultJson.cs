using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Adapter.Host;

/// <summary>Host <c>tools/call</c> result JSON, including MRTR pass-through.</summary>
internal static class HostToolResultJson
{
    public static JsonNode ToNode(McpInvocationResponse response)
    {
        if (ToolsetMrtrBridge.TryGetInputRequiredResult(response, out var inputRequired) && inputRequired is not null)
        {
            return JsonSerializer.SerializeToNode(inputRequired, McpJsonUtilities.DefaultOptions)
                   ?? new JsonObject();
        }

        return InvocationResponseEncoder.ToNode(response);
    }
}
