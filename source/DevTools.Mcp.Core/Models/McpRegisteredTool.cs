using DevTools.Mcp.Core.Protocol;

namespace DevTools.Mcp.Core;

public sealed record McpRegisteredTool
{
    public required string Id { get; init; }

    public required McpToolDescriptor Descriptor { get; init; }

    public required McpPrimitiveBinding Binding { get; init; }
}
