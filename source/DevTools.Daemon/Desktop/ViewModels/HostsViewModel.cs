using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DevTools.Daemon.Desktop.Helpers;
using DevTools.Daemon.Desktop.Models;
using DevTools.Ipc;
using DevTools.Daemon.Mcp.Processes;

namespace DevTools.Daemon.Desktop.ViewModels;

public partial class HostsViewModel : ObservableObject
{
    private const string StatusUnknown = "Unknown";
    private const string StatusConnected = "Connected";
    private const string StatusDiscovered = "Discovered";

    private readonly IHostConnections _sessions;
    private readonly IMcpPipeScanner _pipeScanner;

    [ObservableProperty]
    public partial int Count { get; set; }

    public ObservableCollection<HostInstance> Rows { get; } = [];

    public HostsViewModel(IHostConnections sessions, IMcpPipeScanner pipeScanner)
    {
        _sessions = sessions;
        _pipeScanner = pipeScanner;
        Refresh();
        _sessions.Changed += () => UiDispatchHelper.Post(Refresh);
    }

    public void Refresh()
    {
        Rows.Clear();

        var connectedPids = new HashSet<int>();
        foreach (var entry in _sessions.Catalog.List())
        {
            connectedPids.Add(entry.Instance.ProcessId);
            Rows.Add(new HostInstance(
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

            Rows.Add(new HostInstance(host, version, pid, StatusDiscovered));
        }
    }
}
