using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Server.Contracts;

/// <summary>
/// Compact JSON <see cref="CallToolResult"/> envelopes for daemon tools that emit structured output.
/// </summary>
/// <remarks>
/// SDK 2.0 workaround (2026-08): do not set <c>UseStructuredContent</c> on affected tools yet.
/// With <c>UseStructuredContent = true</c>, the SDK advertises <c>outputSchema</c> on <c>tools/list</c>.
/// Auto-schemas from <see cref="JsonElement"/> / open <c>object</c> members (e.g.
/// <see cref="SearchCapabilityItem.InputSchema"/>) serialize as <c>outputSchema: true</c> or otherwise
/// fail strict client validation. Cursor then drops the entire tool list (0 tools) while prompts still load.
/// Emit <see cref="CallToolResult.StructuredContent"/> manually instead and leave <c>OutputSchema</c> unset.
/// TODO(sdk-2.0-clients): when MCP clients adopt SDK 2.0 structured output, re-enable
/// <c>UseStructuredContent</c> with explicit hand-authored <c>OutputSchema</c> objects (not inferred from
/// <see cref="JsonElement"/>-bearing DTOs) and drop this manual path where no longer needed.
/// </remarks>
internal static class DynamicToolCallResults
{
    public static CallToolResult Result<T>(T value, object? structured = null) => new()
    {
        Content = [new TextContentBlock { Text = JsonSerializer.Serialize(value, McpJsonUtilities.DefaultOptions) }],
        StructuredContent = structured is null
            ? null
            : JsonSerializer.SerializeToElement(structured, McpJsonUtilities.DefaultOptions)
    };

    public static CallToolResult Error(string type, string message) =>
        Result(new { ok = false, error = new { type, message } });
}
