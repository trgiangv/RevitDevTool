using System.Text.Json.Serialization;

namespace RevitDevTool.Contracts;

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
