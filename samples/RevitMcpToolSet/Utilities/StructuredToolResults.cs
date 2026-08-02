using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace RevitMcpToolSet.Utilities;

internal static class StructuredToolResults
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static CallToolResult Create(object structured, string summary) => new()
    {
        StructuredContent = JsonSerializer.SerializeToElement(structured, JsonOptions),
        Content = [new TextContentBlock { Text = summary }]
    };
}
