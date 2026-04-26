namespace DevTools.Entities.Configs;

[Serializable]
public sealed class McpRegistryConfig
{
    public List<string> DotnetPaths { get; set; } = [];
    public List<string> PythonToolsetPaths { get; set; } = [];
}
