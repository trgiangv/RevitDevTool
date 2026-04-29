using System.Text.Json.Serialization;
namespace DevTools.Settings.Configs;

[Serializable]
public class McpRegistryConfig
{
    [JsonPropertyName("dotnetToolsetPaths")]
    public List<string> DotnetPaths { get; set; } = [];
    
    [JsonPropertyName("pythonToolsetPaths")]
    public List<string> PythonToolsetPaths { get; set; } = [];
}
