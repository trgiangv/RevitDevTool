using ModelContextProtocol.Protocol;
namespace DevTool.McpParser.Models;

public sealed record McpRegisteredResource
{
    public required string Id { get; init; }
    public Resource? ProtocolResource { get; init; }
    public ResourceTemplate? ProtocolTemplate { get; init; }
    public required McpPrimitiveBinding Binding { get; init; }
}
