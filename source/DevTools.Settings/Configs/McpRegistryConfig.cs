namespace DevTools.Settings.Configs;

[Serializable]
public class McpRegistryConfig
{
    public List<string> DotnetPaths { get; set; } = [];
    public List<string> PythonToolsetPaths { get; set; } = [];
}
