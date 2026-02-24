namespace RevitDevTool.CodeExecute.Models;

/// <summary>
/// Execution mode for code execution.
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// Script execution (Python or F#)
    /// </summary>
    Script,

    /// <summary>
    /// .NET assembly execution
    /// </summary>
    Assembly,

    /// <summary>
    /// Python script execution (directly via PythonNet)
    /// </summary>
    Python,

    /// <summary>
    /// F# script execution (via FSharp.Compiler.Service)
    /// </summary>
    FSharp
}