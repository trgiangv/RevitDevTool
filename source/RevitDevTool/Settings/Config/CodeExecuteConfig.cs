using System.Text.Json.Serialization;

namespace RevitDevTool.Settings.Config;

/// <summary>
/// Configuration for persisting code execution settings (DotNet assemblies and script folders)
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
    /// List of folders to load scripts from (Python .py and F# .fsx)
    /// </summary>
    [JsonPropertyName("ScriptFolderPaths")]
    public List<string> ScriptFolderPaths { get; set; } = [];
}