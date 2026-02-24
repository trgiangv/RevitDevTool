using RevitDevTool.Bridge.Enums;

namespace RevitDevTool.Bridge;

/// <summary>
/// Execution-level settings: "how to run" the batch.
/// Lives in the JSON config <c>strategy</c> section. CLI can override
/// <see cref="ConnectionMode"/>, <see cref="Mode"/>, and <see cref="ParallelCount"/>.
/// </summary>
public sealed class ExecutionStrategy
{
    public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.Attach;
    public ProcessingMode Mode { get; set; } = ProcessingMode.SequentialMulti;
    public int ParallelCount { get; set; } = 2;
    public int LaunchTimeoutSeconds { get; set; } = 120;
    public int TimeoutPerFileSeconds { get; set; } = 1800;
}
