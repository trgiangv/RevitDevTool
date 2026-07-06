using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevTools.Ipc;

/// <summary>
/// Typed params for tools/call bridge request.
/// </summary>
[UsedImplicitly]
public sealed class McpToolsCallParams
{
    [JsonPropertyName(IpcPropertyNames.Name)]
    public required string Name { get; init; }

    [JsonPropertyName(IpcPropertyNames.Arguments)]
    public Dictionary<string, JsonElement>? Arguments { get; init; }
}

/// <summary>
/// Typed params for prompts/get bridge request.
/// </summary>
[UsedImplicitly]
public sealed class McpPromptsGetParams
{
    [JsonPropertyName(IpcPropertyNames.Name)]
    public required string Name { get; init; }

    [JsonPropertyName(IpcPropertyNames.Arguments)]
    public Dictionary<string, JsonElement>? Arguments { get; init; }
}

/// <summary>
/// Typed params for resources/read bridge request.
/// </summary>
[UsedImplicitly]
public sealed class McpResourcesReadParams
{
    [JsonPropertyName(IpcPropertyNames.Uri)]
    public required string Uri { get; init; }
}
