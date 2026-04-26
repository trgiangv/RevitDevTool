using ModelContextProtocol.Protocol;
namespace DevTools.McpParser.Models;

public sealed record McpRegisteredPrompt
{
    public required string Id { get; init; }
    public required Prompt ProtocolPrompt { get; init; }
    public required McpPrimitiveBinding Binding { get; init; }
}
