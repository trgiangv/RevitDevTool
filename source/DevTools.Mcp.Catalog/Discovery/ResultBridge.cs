using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace DevTools.Mcp.Discovery;

/// <summary>
/// Turns what the host SDK returns for an isolated toolset tool into the host <see cref="CallToolResult"/>.
/// </summary>
public static class ResultBridge
{
    public static CallToolResult ToHostCallToolResult(CallToolResult sdkResult, bool returnsToolsetProtocol) =>
        returnsToolsetProtocol ? ReadToolsetProtocolResult(sdkResult) : sdkResult;

    private static CallToolResult ReadToolsetProtocolResult(CallToolResult sdkResult)
    {
        if (sdkResult.Content is not [TextContentBlock { Text: var json }])
            return sdkResult;

        using var document = JsonDocument.Parse(json);
        var element = document.RootElement;
        if (HasProperty(element, McpSpecKeys.ToolResult.Content))
            return Read<CallToolResult>(element);

        return HasProperty(element, McpSpecKeys.Content.Type)
            ? new CallToolResult { Content = [Read<ContentBlock>(element)] }
            : sdkResult;
    }

    private static bool HasProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out _);

    private static T Read<T>(JsonElement element)
    {
        try
        {
            return element.Deserialize<T>(ToolHelpers.ProtocolOptions)
                   ?? throw new JsonException($"MCP {typeof(T).Name} JSON was null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("MCP tool result JSON did not match the SDK contract.", ex);
        }
    }
}
