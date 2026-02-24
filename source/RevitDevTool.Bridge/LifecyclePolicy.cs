using MessagePack;

namespace RevitDevTool.Bridge;

/// <summary>
/// Controls what happens to the host application and document after a job completes.
/// Separated from <see cref="OpenOptions"/> because lifecycle != open configuration.
/// </summary>
[MessagePackObject]
public sealed partial class LifecyclePolicy
{
    [Key(0)] public bool CloseDocument { get; set; } = true;

    /// <summary>
    /// Whether to shut down the host application after processing.
    /// For Revit, this triggers a graceful shutdown via <c>Process.CloseMainWindow()</c>.
    /// </summary>
    [Key(1)] public bool CloseHost { get; set; }
}
