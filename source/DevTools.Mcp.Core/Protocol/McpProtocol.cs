using System.Text.Json.Nodes;

namespace DevTools.Mcp.Core.Protocol;

/// <summary>Host wire protocol version helpers (<c>2026-07-28</c>).</summary>
public static class McpProtocol
{
    public static string? GetVersion(JsonObject? parameters)
    {
        if (parameters?[McpSpecKeys.Meta.Key] is not JsonObject meta)
            return null;

        return meta[McpSpecKeys.Meta.ProtocolVersion]?.GetValue<string>();
    }

    public static bool IsCurrent(string? protocolVersion) =>
        string.Equals(protocolVersion, McpSpecKeys.ProtocolVersions.Current, StringComparison.Ordinal);

    /// <summary>
    /// Ensures per-request <c>_meta/io.modelcontextprotocol/protocolVersion</c> is set for host wire calls.
    /// </summary>
    public static void EnsureCurrentProtocolMeta(ModelContextProtocol.Protocol.RequestParams parameters)
    {
        parameters.Meta ??= new JsonObject();
        if (parameters.Meta[McpSpecKeys.Meta.ProtocolVersion] is null)
            parameters.Meta[McpSpecKeys.Meta.ProtocolVersion] = McpSpecKeys.ProtocolVersions.Current;
    }
}
