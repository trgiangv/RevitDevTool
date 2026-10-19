namespace DevTools.Daemon.Desktop.Models;

public sealed record HostInstance(string Host, string Version, int Pid, string Status);
