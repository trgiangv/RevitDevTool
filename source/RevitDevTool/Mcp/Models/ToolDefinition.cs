using System.Text.Json.Serialization;
namespace RevitDevTool.Mcp.Models;

public sealed record InputSchema
{
    [JsonPropertyName("properties")]
    public Dictionary<string, InputSchemaProperty>? Properties { get; init; }

    [JsonPropertyName("required")]
    public List<string>? Required { get; init; }
}

public sealed record InputSchemaProperty
{
    [JsonConstructor]
    public InputSchemaProperty() { }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
