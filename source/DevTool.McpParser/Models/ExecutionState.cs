using System.Text.Json.Serialization;
namespace DevTool.McpParser.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ExecutionState>))]
public enum ExecutionState
{
    Queued,
    Preparing,
    Running,
    Completed,
    Failed,
    Cancelled
}
