using System.Text.Json.Serialization;

namespace RevitDevTool.Settings.Config;

[Serializable]
public sealed class McpRegistryConfig
{
    [JsonPropertyName("DotnetPaths")]
    public List<string> DotnetPaths { get; set; } = [];

    [JsonPropertyName("PythonToolsetPaths")]
    public List<string> PythonToolsetPaths { get; set; } = [];
}
