using System.Text.Json;
using DevTools.McpParser.Models;
using ModelContextProtocol.Protocol;
namespace DevTools.Execution.External.Mcp.Execution;

internal static class PythonResultParser
{
    public static CallToolResult ParseCallToolResult(string resultJson)
    {
        using var doc = JsonDocument.Parse(resultJson);
        var root = doc.RootElement;

        return root.ValueKind switch
        {
            JsonValueKind.Null => new CallToolResult(),
            JsonValueKind.String => new CallToolResult { Content = [new TextContentBlock { Text = root.GetString() ?? string.Empty }] },
            JsonValueKind.Array => new CallToolResult { Content = ParseContentBlocks(root) },
            JsonValueKind.Object when root.TryGetProperty(McpPropertyNames.Content, out var contentProp) =>
                new CallToolResult { Content = ParseContentBlocks(contentProp) },
            _ => new CallToolResult { Content = [new TextContentBlock { Text = resultJson }] },
        };
    }

    private static List<ContentBlock> ParseContentBlocks(JsonElement array)
    {
        var blocks = new List<ContentBlock>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(IpcPropertyNames.Type, out var typeProp)
                && typeProp.GetString() is McpPropertyNames.Text
                && element.TryGetProperty(McpPropertyNames.Text, out var textProp))
            {
                blocks.Add(new TextContentBlock { Text = textProp.GetString() ?? string.Empty });
            }
            else
            {
                blocks.Add(new TextContentBlock { Text = element.GetRawText() });
            }
        }

        return blocks.Count > 0 ? blocks : [new TextContentBlock { Text = array.GetRawText() }];
    }
}
