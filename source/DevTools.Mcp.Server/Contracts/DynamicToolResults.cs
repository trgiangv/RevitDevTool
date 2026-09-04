using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Server.Contracts;

/// <summary>
/// Compact JSON <see cref="CallToolResult"/> envelopes for tools that emit structured output.
/// </summary>
/// <remarks>
/// SDK 2.0 workaround (2026-08): do not set <c>UseStructuredContent</c> on affected tools yet.
/// With <c>UseStructuredContent = true</c>, the SDK advertises <c>outputSchema</c> on <c>tools/list</c>.
/// Auto-schemas from <see cref="JsonElement"/> / open <c>object</c> members (e.g.
/// <see cref="SearchCapabilityItem.InputSchema"/>) serialize as <c>outputSchema: true</c> or otherwise
/// fail strict client validation. Cursor then drops the entire tool list (0 tools) while prompts still load.
/// Emit <see cref="CallToolResult.StructuredContent"/> manually instead and leave <c>OutputSchema</c> unset.
/// See 0027 / 0031 — UseStructuredContent deferred.
/// </remarks>
internal static class DynamicToolResults
{
    public static CallToolResult Result<T>(T value, JsonTypeInfo<T> typeInfo, bool structured = false) => new()
    {
        // The top-level DTO is source-generated, but invoke results intentionally contain
        // protocol objects behind object-typed members. Use the chained tool options so
        // the server context and MCP protocol context participate in one serialization.
        Content = [new TextContentBlock { Text = JsonSerializer.Serialize(value, McpToolJson.Options) }],
        StructuredContent = structured ? JsonSerializer.SerializeToElement(value, McpToolJson.Options) : null
    };

    public static CallToolResult Json(string text, JsonElement? structured = null) => new()
    {
        Content = [new TextContentBlock { Text = text }],
        StructuredContent = structured
    };

    public static CallToolResult Error(string type, string message) =>
        Json(new JsonObject
        {
            ["ok"] = false,
            ["error"] = new JsonObject
            {
                ["type"] = type,
                ["message"] = message,
            },
        }.ToJsonString());
}
