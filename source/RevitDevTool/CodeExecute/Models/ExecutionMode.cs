namespace RevitDevTool.CodeExecute.Models;

/// <summary>
/// Execution mode for code execution.
/// Changed from "Dotnet" to "DotNet" to better represent the .NET ecosystem.
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// .NET assembly execution (was "Dotnet")
    /// </summary>
    DotNet,

    /// <summary>
    /// Python script execution (directly via PythonNet, no Dynamo)
    /// </summary>
    Python
}