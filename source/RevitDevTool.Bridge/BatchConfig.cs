namespace RevitDevTool.Bridge;

/// <summary>
/// Root configuration for batch processing.
/// Serialized as JSON — shared contract between CLI, UI, and IPC.
/// </summary>
public sealed class BatchConfig
{
    /// <summary>
    /// Execution-level settings: connection mode, processing mode, timeouts.
    /// CLI args can override <c>connectionMode</c>, <c>mode</c>, and <c>parallelCount</c>.
    /// </summary>
    public ExecutionStrategy Strategy { get; set; } = new();

    /// <summary>
    /// Fallback values for any field not specified in a <see cref="FileEntry"/>.
    /// Uses the same nullable schema as <see cref="FileEntry"/> for consistency.
    /// </summary>
    public JobDefaults Defaults { get; set; } = new();
    public List<FileEntry> Files { get; set; } = [];
}
