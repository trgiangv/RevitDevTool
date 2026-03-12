using ModelContextProtocol.Protocol;
namespace RevitDevTool.Mcp.Parser.Models;

public sealed record McpRegisteredPrompt
{
    public required string Id { get; init; }
    public required Prompt ProtocolPrompt { get; init; }
    public required McpPrimitiveBinding Binding { get; init; }
}
