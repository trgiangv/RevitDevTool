using System.Text.Json;
using DevTools.Mcp.Core.Invocation;

namespace DevTools.Mcp.Core.Protocol;

/// <summary>Prepares <see cref="McpInvocationResponse"/> content before SDK <c>tools/call</c> serialization.</summary>
public static class InvocationResponseEncoder
{
    private const int MaxPreviewLength = 240;
    private const string Ellipsis = "...";
    private static readonly int PreviewPrefixLength = MaxPreviewLength - Ellipsis.Length;

    public static McpInvocationResponse PrepareForWire(McpInvocationResponse response)
    {
        var content = new List<McpContent>();
        foreach (var block in response.Content)
        {
            if (block is McpTextContent text && string.IsNullOrEmpty(text.Text))
            {
                if (response.StructuredContent is { } structured)
                    content.Add(text with { Text = PreviewStructured(structured) });
                continue;
            }

            content.Add(block);
        }

        if (content.Count == 0 && response.StructuredContent is { } onlyStructured)
            content.Add(new McpTextContent(PreviewStructured(onlyStructured)));

        return response with { Content = content };
    }

    public static string PreviewStructured(JsonElement structured)
    {
        var raw = structured.GetRawText();
        return raw.Length <= MaxPreviewLength ? raw : raw[..PreviewPrefixLength] + Ellipsis;
    }
}
