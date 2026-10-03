using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DevTools.Ipc;
using DevTools.Daemon.Mcp.Processes;

namespace DevTools.Daemon.Desktop;

public sealed record HostRow(string Host, string Version, int Pid, string Status);

public partial class HostInstances : ObservableObject
{
    private const string StatusUnknown = "Unknown";
    private const string StatusConnected = "Connected";
    private const string StatusDiscovered = "Discovered";

    private readonly IProcessSessions _sessions;
    private readonly IMcpPipeScanner _pipeScanner;

    [ObservableProperty]
    public partial int Count { get; set; }

    public ObservableCollection<HostRow> Rows { get; } = [];

    public HostInstances(IProcessSessions sessions, IMcpPipeScanner pipeScanner)
    {
        _sessions = sessions;
        _pipeScanner = pipeScanner;
        Refresh();
        _sessions.Changed += () => UiDispatch.Post(Refresh);
    }

    public void Refresh()
    {
        Rows.Clear();

        var connectedPids = new HashSet<int>();
        foreach (var entry in _sessions.Catalog.List())
        {
            connectedPids.Add(entry.Instance.ProcessId);
            Rows.Add(new HostRow(
                entry.Instance.HostApp ?? StatusUnknown,
                entry.Instance.VersionNumber,
                entry.Instance.ProcessId,
                StatusConnected));
        }

        Count = connectedPids.Count;

        foreach (var pipe in _pipeScanner.Discover())
        {
            if (!HostPipeName.TryParse(pipe, out var host, out var version, out var pid))
                continue;
            if (connectedPids.Contains(pid))
                continue;

            Rows.Add(new HostRow(host, version, pid, StatusDiscovered));
        }
    }
}
