using System.Text.Json.Serialization;

namespace RevitDevTool.Settings.Config;

/// <summary>
/// Configuration for persisting code execution settings (CSharp assemblies and Python scripts)
/// </summary>
[Serializable]
public sealed class CodeExecuteConfig
{
    /// <summary>
    /// Current execution mode (CSharp or Python)
    /// </summary>
    [JsonPropertyName("ExecutionMode")]
    public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.CSharp;

    /// <summary>
    /// List of assembly file paths for CSharp mode
    /// </summary>
    [JsonPropertyName("CSharpAssemblyPaths")]
    public List<string> CSharpAssemblyPaths { get; set; } = [];

    /// <summary>
    /// Python script groups for Python mode
    /// </summary>
    [JsonPropertyName("PythonGroups")]
    public List<PythonGroup> PythonGroups { get; set; } = [];
}

/// <summary>
/// Execution mode enum
/// </summary>
public enum ExecutionMode
{
    CSharp,
    Python
}

/// <summary>
/// Represents a group of Python scripts
/// </summary>
[Serializable]
public sealed class PythonGroup
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Scripts")]
    public List<string> Scripts { get; set; } = [];
}
