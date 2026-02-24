namespace RevitDevTool.Bridge;

public sealed class BatchResult
{
    public List<JobResult> Results { get; set; } = [];
    public int TotalFiles { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public long TotalDurationMs { get; set; }
}
