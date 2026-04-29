using System.Text.Json.Serialization;
namespace DevTools.Settings.Configs;

/// <summary>
/// Configuration for persisting code execution settings (DotNet assemblies and script folders)
/// </summary>
[Serializable]
public class ExecutionConfig
{
    /// <summary>
    /// List of assembly file paths
    /// </summary>
    [JsonPropertyName("dotnetAssemblyPaths")]
    public List<string> DotnetAssemblyPaths { get; set; } = [];

    /// <summary>
    /// List of folders to load scripts from (Python .py and F# .fsx)
    /// </summary>
    [JsonPropertyName("scriptFolderPaths")]
    public List<string> ScriptFolderPaths { get; set; } = [];
}
