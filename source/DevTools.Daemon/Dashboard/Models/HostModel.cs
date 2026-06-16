namespace DevTools.Daemon.Dashboard.Models;

public sealed class HostModel
{
    public string Host { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public int Pid { get; init; }
    public string Status { get; init; } = string.Empty;
}
