namespace DevTools.Execution.Configs;

/// <summary>
/// Configuration for persisting code execution settings (DotNet assemblies and script folders)
/// </summary>
[Serializable]
public class ExecutionConfig
{
    /// <summary>
    /// List of assembly file paths
    /// </summary>
    public List<string> DotnetAssemblyPaths { get; set; } = [];

    /// <summary>
    /// List of folders to load scripts from (Python .py and F# .fsx)
    /// </summary>
    public List<string> ScriptFolderPaths { get; set; } = [];
}