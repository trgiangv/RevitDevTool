using DevTools.McpParser.Models;
namespace RevitDevTool.Execution.Models;

/// <summary>
/// Root node - represents an Assembly (.dll) or Root Folder for scripts
/// </summary>
public sealed class ExecutionNodeRoot : ExecutionNodeBase
{
    /// <summary>
    /// Path to the root (assembly file or folder)
    /// </summary>
    public required string RootPath { get; init; }

    /// <summary>
    /// Provider type (DotNet or Python)
    /// </summary>
    public required ExecutionMode ProviderType { get; init; }
}

/// <summary>
/// Intermediate node - represents a Namespace or SubFolder
/// </summary>
public sealed class ExecutionNodeIntermediate : ExecutionNodeBase
{
    /// <summary>
    /// Full path to namespace or folder
    /// </summary>
    public required string FullPath { get; init; }
}

/// <summary>
/// Execute node - represents an IExternalCommand or Python Script
/// </summary>
public sealed class ExecutionNode : ExecutionNodeBase
{
    /// <summary>
    /// Full path to the executable (class name or script path)
    /// </summary>
    public required string ExecutablePath { get; init; }

    /// <summary>
    /// Provider type (DotNet or Python)
    /// </summary>
    public required ExecutionMode ProviderType { get; init; }

    /// <summary>
    /// Source file path (for open location)
    /// </summary>
    public string? SourceFilePath { get; init; }
}