using System.Text.Json.Serialization;

namespace RevitDevTool.Execution.Models;

/// <summary>
/// Execution mode for code execution.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
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
    /// Python tool execution
    /// </summary>
    Python,

    /// <summary>
    /// F# script execution (via FSharp.Compiler.Service)
    /// </summary>
    FSharp
}