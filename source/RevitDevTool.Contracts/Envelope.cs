using System.Text.Json;

namespace RevitDevTool.Contracts;

public sealed record Envelope
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string ExecutionId { get; init; } = string.Empty;
    public string Kind { get; init; } = BridgeMessageKinds.Request;
    public string Action { get; init; } = string.Empty;
    public bool IsError { get; init; }
    public JsonElement? Body { get; init; }
}
