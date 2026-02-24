using RevitDevTool.Bridge.Enums;

namespace RevitDevTool.Bridge;

/// <summary>
/// Unified, fully-resolved execution descriptor.
/// Merges all inputs (JSON config + CLI overrides) into a single source of truth.
/// Both the Console CLI and the Processor UI produce this type.
/// </summary>
public sealed class ExecutionPlan
{
    public ConnectionMode ConnectionMode { get; init; }
    public ProcessingMode ProcessingMode { get; init; } = ProcessingMode.SequentialMulti;
    public int ParallelInstanceCount { get; init; } = 2;
    public int LaunchTimeoutSeconds { get; init; } = 120;
    public int TimeoutPerFileSeconds { get; init; } = 1800;
    public List<ResolvedJob> Jobs { get; init; } = [];
}
