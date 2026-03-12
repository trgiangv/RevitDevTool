using ModelContextProtocol.Protocol;
namespace RevitDevTool.Mcp.Parser.Models;

public sealed record McpRegisteredTool
{
    public required string Id { get; init; }
    public required Tool ProtocolTool { get; init; }
    public required McpPrimitiveBinding Binding { get; init; }
}
