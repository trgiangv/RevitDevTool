using RevitDevTool.Bridge.Enums;

namespace RevitDevTool.Desktop.Models;

public sealed class ProcessorRunOptions
{
    public ProcessingMode? ProcessingMode { get; set; }
    public int? ParallelCount { get; set; }
    public bool ForceLaunch { get; set; }
}
