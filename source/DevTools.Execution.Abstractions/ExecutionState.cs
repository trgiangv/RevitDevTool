using System.Text.Json.Serialization;

namespace DevTools.Execution.Abstractions;

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
