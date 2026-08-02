using System.Text.Json;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
namespace DevTools.Mcp.Adapter.Execution;

internal static class PythonResultParser
{
    public static CallToolResult ParseCallToolResult(string resultJson)
    {
        using var document = JsonDocument.Parse(resultJson);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(McpSpecKeys.ResultType.Key, out var resultType) &&
            resultType.GetString() == McpSpecKeys.ResultType.InputRequired)
        {
            try
            {
                var inputRequired = JsonSerializer.Deserialize<InputRequiredResult>(
                    root.GetRawText(),
                    McpJsonUtilities.DefaultOptions)
                    ?? throw new InvalidOperationException("Python MCP input-required result was null.");
                throw new InputRequiredException(inputRequired);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    "Python MCP input-required result was malformed.",
                    ex);
            }
        }

        return root.ValueKind switch
        {
            JsonValueKind.Null => new CallToolResult(),
            JsonValueKind.String => new CallToolResult { Content = [new TextContentBlock { Text = root.GetString() ?? string.Empty }] },
            JsonValueKind.Array => new CallToolResult { Content = ParseContentBlocks(root) },
            JsonValueKind.Object when root.TryGetProperty(McpSpecKeys.ToolResult.Content, out _) =>
                JsonSerializer.Deserialize<CallToolResult>(root.GetRawText(), McpJsonUtilities.DefaultOptions)
                ?? throw new InvalidOperationException("Python MCP result was null."),
            _ => throw new InvalidOperationException("Python MCP tool result must be null, text, a content array, a CallToolResult object, or an InputRequiredResult object.")
        };
    }

    private static List<ContentBlock> ParseContentBlocks(JsonElement array)
    {
        var blocks = new List<ContentBlock>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Python MCP content entries must be content-block objects.");

            blocks.Add(JsonSerializer.Deserialize<ContentBlock>(element.GetRawText(), McpJsonUtilities.DefaultOptions)
                ?? throw new InvalidOperationException("Python MCP content entry was null."));
        }

        return blocks;
    }
}
