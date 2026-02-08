using System.Text.Json.Serialization;

namespace RevitDevTool.Settings.Config;

/// <summary>
/// Configuration for persisting code execution settings (Dotnet assemblies and Python scripts)
/// </summary>
[Serializable]
public sealed class CodeExecuteConfig
{
    /// <summary>
    /// List of assembly file paths
    /// </summary>
    [JsonPropertyName("DotnetAssemblyPaths")]
    public List<string> DotnetAssemblyPaths { get; set; } = [];

    /// <summary>
    /// List of folders to load Python scripts from
    /// </summary>
    [JsonPropertyName("PythonFolderPaths")]
    public List<string> PythonFolderPaths { get; set; } = [];
}