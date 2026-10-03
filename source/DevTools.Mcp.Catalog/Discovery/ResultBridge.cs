using System.Text.Json;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Discovery;

/// <summary>
/// Single JSON crossing from an isolated toolset ALC into the host SDK <see cref="CallToolResult"/>.
/// </summary>
public static class ResultBridge
{
    public static CallToolResult ToHostCallToolResult(object? raw, JsonElement? outputSchema)
    {
        if (raw is null)
            return new CallToolResult { Content = [] };

        if (raw is CallToolResult hostResult && ReferenceEquals(raw.GetType(), typeof(CallToolResult)))
            return hostResult;

        if (raw is ContentBlock hostBlock)
        {
            return new CallToolResult
            {
                Content = [hostBlock],
            };
        }

        var element = JsonSerializer.SerializeToElement(raw, raw.GetType(), ToolHelpers.RuntimeJsonOptions);
        if (TryReadCallToolResult(element, out var bridged))
            return bridged;

        if (TryReadContentBlock(element, out var contentBlock))
        {
            return new CallToolResult
            {
                Content = [contentBlock],
            };
        }

        return MapPlainResult(raw, element, outputSchema);
    }

    private static bool HasProperty(JsonElement element, string name, string alternate) =>
        element.TryGetProperty(name, out _) || element.TryGetProperty(alternate, out _);

    private static bool TryReadCallToolResult(JsonElement element, out CallToolResult result)
    {
        result = null!;
        if (element.ValueKind != JsonValueKind.Object || !HasProperty(element, McpSpecKeys.ToolResult.Content, McpSpecKeys.ToolResult.ContentPascal))
            return false;

        try
        {
            result = element.Deserialize<CallToolResult>(ToolHelpers.ProtocolOptions)!;
            return result is not null;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("MCP tool result JSON did not match the SDK contract.", ex);
        }
    }

    private static bool TryReadContentBlock(JsonElement element, out ContentBlock block)
    {
        block = null!;
        if (element.ValueKind != JsonValueKind.Object || !HasProperty(element, McpSpecKeys.Content.Type, McpSpecKeys.Content.TypePascal))
            return false;

        try
        {
            block = element.Deserialize<ContentBlock>(ToolHelpers.ProtocolOptions)!;
            return block is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static CallToolResult MapPlainResult(object raw, JsonElement element, JsonElement? outputSchema)
    {
        JsonElement? structured = outputSchema is null ? null : element.Clone();
        switch (raw)
        {
            case string text:
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = text }],
                    StructuredContent = structured,
                };
            case bool isError:
                return new CallToolResult
                {
                    IsError = isError,
                    Content = [new TextContentBlock { Text = element.GetRawText() }],
                    StructuredContent = structured,
                };
            default:
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = element.GetRawText() }],
                    StructuredContent = structured,
                };
        }
    }
}
