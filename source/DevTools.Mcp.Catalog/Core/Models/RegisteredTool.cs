using ModelContextProtocol.Protocol;
namespace DevTools.Mcp.Core.Models;

public sealed record RegisteredTool
{
    public required string Id { get; init; }

    public required Tool Descriptor { get; init; }

    public required PrimitiveBinding Binding { get; init; }
}
