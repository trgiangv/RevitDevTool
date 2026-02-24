using RevitDevTool.Bridge.Enums;

namespace RevitDevTool.Console;

/// <summary>
/// Nullable CLI overrides. Null means "not specified by user, use config value."
/// Only fields that make sense as ad-hoc operational overrides live here.
/// </summary>
public sealed class CliOverrides
{
    public ProcessingMode? ProcessingMode { get; init; }
    public int? ParallelInstanceCount { get; init; }
    public bool? Launch { get; init; }
}
