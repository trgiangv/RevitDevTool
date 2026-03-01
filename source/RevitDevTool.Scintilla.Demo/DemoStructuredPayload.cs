namespace RevitDevTool.Scintilla.Demo;

internal sealed class DemoStructuredPayload
{
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string DocsUrl { get; set; } = string.Empty;
    public string SupportMail { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public DateTime TimestampUtc { get; set; }
    public DemoStructuredDetails Details { get; set; } = new();
}

internal sealed class DemoStructuredDetails
{
    public string HostName { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public int[] Thresholds { get; set; } = Array.Empty<int>();
}
