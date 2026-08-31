using ModelContextProtocol.Protocol;
namespace DevTools.Mcp.Core.Models;

public sealed record McpRegisteredTool
{
    public required string Id { get; init; }

    public required Tool Descriptor { get; init; }

    public required McpPrimitiveBinding Binding { get; init; }
}
