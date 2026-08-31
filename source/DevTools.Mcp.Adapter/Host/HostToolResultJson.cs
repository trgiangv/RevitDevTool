using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Mcp.Core.Invocation;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol;

namespace DevTools.Mcp.Adapter.Host;

/// <summary>Host <c>tools/call</c> result JSON, including MRTR pass-through.</summary>
internal static class HostToolResultJson
{
    public static JsonNode ToNode(McpInvocationResponse response)
    {
        if (response.InputRequired is not null)
        {
            return JsonSerializer.SerializeToNode(response.InputRequired, McpJsonUtilities.DefaultOptions)
                   ?? new JsonObject();
        }

        if (ToolsetMrtrBridge.TryGetInputRequiredResult(response, out var legacyInputRequired) && legacyInputRequired is not null)
        {
            return JsonSerializer.SerializeToNode(legacyInputRequired, McpJsonUtilities.DefaultOptions)
                   ?? new JsonObject();
        }

        return InvocationResponseEncoder.ToNode(response);
    }
}
