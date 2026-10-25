using CommunityToolkit.Mvvm.ComponentModel;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Desktop.Models;
using DevTools.Daemon.Desktop.Updates;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Daemon.Tunnel;
using System.Reflection;

namespace DevTools.Daemon.Desktop.ViewModels;

public partial class ShellViewModel(
    IAuthService authService,
    IHostConnections sessions,
    IMcpPipeScanner pipeScanner,
    UserSettingsStore settings,
    ITunnelStatusProvider tunnelStatus,
    UpdatesViewModel? updates = null) : ObservableObject
{
    private const string DefaultVersion = "1.0.0";
    public const int UpdatesTabIndex = 3;

    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    public OverviewViewModel Overview { get; } = new(authService, tunnelStatus);
    public HostsViewModel Hosts { get; } = new(sessions, pipeScanner);
    public SettingsViewModel Settings { get; } = new(settings);
    public UpdatesViewModel? Updates { get; } = updates;
    public string Version { get; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? DefaultVersion;

    partial void OnSelectedTabIndexChanged(int value)
    {
        switch (value)
        {
            case 1:
                Hosts.Refresh();
                break;
            case 2:
                Settings.ReloadAutoStart();
                break;
            case UpdatesTabIndex:
                _ = Updates?.LoadAsync();
                break;
        }
    }
}
