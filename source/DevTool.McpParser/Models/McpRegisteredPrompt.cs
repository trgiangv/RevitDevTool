using ModelContextProtocol.Protocol;
namespace DevTool.McpParser.Models;

public sealed record McpRegisteredPrompt
{
    public required string Id { get; init; }
    public required Prompt ProtocolPrompt { get; init; }
    public required McpPrimitiveBinding Binding { get; init; }
}
