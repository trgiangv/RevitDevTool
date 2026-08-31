using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DevTools.Daemon.Auth;
using DevTools.Daemon.Dashboard.Models;
using DevTools.Daemon.Hosting;
using DevTools.Mcp.Client;
using DevTools.Mcp.Core.Sessions;
using DevTools.UI.Theme;
using Microsoft.Win32;

namespace DevTools.Daemon.Dashboard;

public partial class DashboardViewModel : ObservableObject
{
    private const string DarkThemeName = "Dark.Blue";
    private const string LightThemeName = "Light.Blue";
    private const string StatusUnknown = "Unknown";
    private const string StatusNotSignedIn = "Not signed in";
    private const string StatusConnected = "Connected";
    private const string StatusDisconnected = "Disconnected";
    private const string StatusDiscovered = "Discovered";
    private const string DefaultVersion = "1.0.0";
    private const string SignInFailedTitle = "Sign In Failed";
    private const string SignInFailedMessage = "Sign in failed.";
    private readonly IAuthService _authService;
    private readonly IHostBroker _hostBroker;
    private readonly IMcpPipeScanner _pipeScanner;
    private readonly DaemonSettings _settings;
    private readonly ITunnelStatusProvider? _tunnelStatus;
    private bool _suppressAutoStartSync;

    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    [ObservableProperty]
    public partial bool IsAuthenticated { get; set; }

    [ObservableProperty]
    public partial string? DisplayName { get; private set; }

    [ObservableProperty]
    public partial string? Email { get; private set; }

    [ObservableProperty]
    public partial ImageSource? AvatarImage { get; private set; }

    [ObservableProperty]
    public partial int HostCount { get; private set; }

    [ObservableProperty]
    public partial string GatewayStatus { get; private set; } = StatusDisconnected;

    [ObservableProperty]
    public partial bool AutoStartEnabled { get; set; }

    [ObservableProperty]
    public partial string Version { get; private set; }

    [ObservableProperty]
    public partial AppTheme Theme { get; set; }
    public List<AppTheme> Themes { get; } = Enum.GetValues<AppTheme>().ToList();
    public ObservableCollection<HostModel> Hosts { get; } = [];

    public DashboardViewModel(
        IAuthService authService,
        IHostBroker hostBroker,
        IMcpPipeScanner pipeScanner,
        DaemonSettings settings,
        ITunnelStatusProvider? tunnelStatus = null)
    {
        _authService = authService;
        _hostBroker = hostBroker;
        _pipeScanner = pipeScanner;
        _settings = settings;
        _tunnelStatus = tunnelStatus;
        Theme = (AppTheme)settings.Theme;

        RefreshAuthState();
        RefreshHostCount();
        RefreshHosts();
        LoadAutoStartState();
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? DefaultVersion;

        _authService.StateChanged += (_, _) =>
            Application.Current.Dispatcher.Invoke(RefreshAuthState);

        _hostBroker.Changed += () => Application.Current.Dispatcher.Invoke(() =>
        {
            RefreshHostCount();
            RefreshHosts();
        });

        if (_tunnelStatus is not null)
        {
            _tunnelStatus.StatusChanged += (_, args) =>
                Application.Current.Dispatcher.Invoke(() => RefreshGatewayStatus(args.Status));
        }

        SystemEvents.UserPreferenceChanged += OnSystemThemeChanged;

        ApplyCurrentTheme(Theme);
        ShowOverview();
    }

    [RelayCommand]
    private void ShowOverview()
    {
        SelectedTabIndex = 0;
    }

    [RelayCommand]
    private void ShowHosts()
    {
        SelectedTabIndex = 1;
        RefreshHosts();
    }

    [RelayCommand]
    private void ShowSettings()
    {
        SelectedTabIndex = 2;
        LoadAutoStartState();
    }

    [RelayCommand]
    private async Task SignIn()
    {
        var result = await _authService.SignInAsync();
        if (!result.Success)
            MessageBox.Show(result.Error ?? SignInFailedMessage, SignInFailedTitle);

        RefreshAuthState();
    }

    [RelayCommand]
    private void EnableAutoStart()
    {
        AutoStart.Enable();
        LoadAutoStartState();
    }

    [RelayCommand]
    private void DisableAutoStart()
    {
        AutoStart.Disable();
        LoadAutoStartState();
    }

    partial void OnAutoStartEnabledChanged(bool value)
    {
        if (_suppressAutoStartSync)
            return;

        if (value)
            AutoStart.Enable();
        else
            AutoStart.Disable();

        _settings.AutoStartEnabled = value;
        _settings.Save();
    }

    partial void OnThemeChanged(AppTheme value)
    {
        _settings.Theme = (DevTools.Settings.Configs.AppTheme)value;
        _settings.Save();
        ApplyCurrentTheme(value);
    }

    private void OnSystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;
        if (Theme != AppTheme.Auto) return;

        Application.Current?.Dispatcher.Invoke(() => ApplyCurrentTheme(AppTheme.Auto));
    }

    private static void ApplyCurrentTheme(AppTheme value)
    {
        var themeName = value switch
        {
            AppTheme.Light => LightThemeName,
            AppTheme.Auto => ControlzEx.Theming.WindowsThemeHelper.AppsUseLightTheme()
                ? LightThemeName
                : DarkThemeName,
            _ => DarkThemeName
        };

        ControlzEx.Theming.ThemeManager.Current.ChangeTheme(Application.Current, themeName);
    }

    private void LoadAutoStartState()
    {
        _suppressAutoStartSync = true;
        AutoStartEnabled = AutoStart.IsEnabled;
        _suppressAutoStartSync = false;
    }

    private void RefreshAuthState()
    {
        IsAuthenticated = _authService.IsAuthenticated;
        DisplayName = _authService.DisplayName;
        Email = _authService.Email;
        AvatarImage = string.IsNullOrWhiteSpace(_authService.AvatarUrl)
            ? null
            : new BitmapImage(new Uri(_authService.AvatarUrl, UriKind.Absolute));

        if (!_authService.IsAuthenticated)
            GatewayStatus = StatusNotSignedIn;
        else if (_tunnelStatus is not null)
            RefreshGatewayStatus(_tunnelStatus.Status);
        else
            GatewayStatus = StatusConnected;
    }

    private void RefreshGatewayStatus(TunnelStatus status)
    {
        GatewayStatus = status switch
        {
            TunnelStatus.Connected => StatusConnected,
            TunnelStatus.Connecting => "Connecting...",
            TunnelStatus.Reconnecting => "Reconnecting...",
            TunnelStatus.Disconnected => StatusDisconnected,
            _ => StatusUnknown,
        };
    }

    private void RefreshHostCount()
    {
        HostCount = _hostBroker.Catalog.List().Count;
    }

    private void RefreshHosts()
    {
        Hosts.Clear();

        var connectedPids = new HashSet<int>();
        foreach (var entry in _hostBroker.Catalog.List())
        {
            connectedPids.Add(entry.Instance.ProcessId);
            Hosts.Add(new HostModel
            {
                Host = entry.Instance.HostApp ?? StatusUnknown,
                Version = entry.Instance.VersionNumber,
                Pid = entry.Instance.ProcessId,
                Status = StatusConnected
            });
        }

        foreach (var pipe in _pipeScanner.Discover())
        {
            if (!HostPipeName.TryParse(pipe, out var host , out var version, out var pid))
                continue;

            if (connectedPids.Contains(pid))
                continue;

            Hosts.Add(new HostModel
            {
                Host = host,
                Version = version,
                Pid = pid,
                Status = StatusDiscovered
            });
        }
    }
}
