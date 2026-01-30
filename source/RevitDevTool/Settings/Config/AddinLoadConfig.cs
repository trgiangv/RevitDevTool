using System.Text.Json.Serialization;

namespace RevitDevTool.Settings.Config;

/// <summary>
/// Configuration for persisting loaded add-in assemblies
/// </summary>
[Serializable]
public sealed class AddinLoadConfig
{
    /// <summary>
    /// List of assembly file paths that should be automatically loaded
    /// </summary>
    [JsonPropertyName("AssemblyPaths")]
    public List<string> AssemblyPaths { get; set; } = [];
}
