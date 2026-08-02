using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DevTools.Mcp.Core.Protocol;

/// <summary>Host wire request for <c>tools/call</c>. MRTR fields pass through to toolsets.</summary>
public sealed record McpInvocationRequest
{
    [JsonPropertyName(McpSpecKeys.Tools.Arguments)]
    public JsonElement? Arguments { get; init; }

    [JsonPropertyName(McpSpecKeys.Tools.InputResponses)]
    public IReadOnlyDictionary<string, JsonElement>? InputResponses { get; init; }

    [JsonPropertyName(McpSpecKeys.Tools.RequestState)]
    public JsonElement? RequestState { get; init; }

    [JsonPropertyName(McpSpecKeys.Tools.ProgressToken)]
    public long? ProgressToken { get; init; }

    [JsonPropertyName(McpSpecKeys.Tools.Meta)]
    public JsonObject? Meta { get; init; }
}
