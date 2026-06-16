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
using DevTools.Daemon.Mcp;
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
    private const char PipeSeparator = '_';
    private const int MinPipeParts = 3;

    private readonly IAuthService _authService;
    private readonly McpEngine _mcpEngine;
    private readonly DaemonSettings _settings;
    private bool _suppressAutoStartSync;

    [ObservableProperty] private object? _currentView;
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private string? _displayName;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _avatarUrl;
    [ObservableProperty] private ImageSource? _avatarImage;
    [ObservableProperty] private int _hostCount;
    [ObservableProperty] private string _gatewayStatus = StatusDisconnected;
    [ObservableProperty] private bool _autoStartEnabled;
    [ObservableProperty] private string _version = string.Empty;
    [ObservableProperty] private AppTheme _theme = AppTheme.Auto;

    public List<AppTheme> Themes { get; } = Enum.GetValues<AppTheme>().ToList();
    public ObservableCollection<HostModel> Hosts { get; } = [];

    public DashboardViewModel(IAuthService authService, McpEngine mcpEngine, DaemonSettings settings)
    {
        _authService = authService;
        _mcpEngine = mcpEngine;
        _settings = settings;
        _theme = settings.Theme;

        RefreshAuthState();
        RefreshHostCount();
        RefreshHosts();
        LoadAutoStartState();
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? DefaultVersion;

        _authService.StateChanged += (_, _) =>
            Application.Current.Dispatcher.Invoke(RefreshAuthState);

        _mcpEngine.InstanceManager.Changed += () => Application.Current.Dispatcher.Invoke(() =>
        {
            RefreshHostCount();
            RefreshHosts();
        });

        SystemEvents.UserPreferenceChanged += OnSystemThemeChanged;

        ApplyCurrentTheme(Theme);
        ShowOverview();
    }

    [RelayCommand]
    private void ShowOverview()
    {
        SelectedTabIndex = 0;
        CurrentView = new Views.OverviewView();
    }

    [RelayCommand]
    private void ShowHosts()
    {
        SelectedTabIndex = 1;
        CurrentView = new Views.HostsView();
        RefreshHosts();
    }

    [RelayCommand]
    private void ShowSettings()
    {
        SelectedTabIndex = 2;
        CurrentView = new Views.SettingsView();
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
        _settings.Theme = value;
        _settings.Save();
        ApplyCurrentTheme(value);
    }

    private void OnSystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;
        if (Theme != AppTheme.Auto) return;

        Application.Current?.Dispatcher.Invoke(() => ApplyCurrentTheme(AppTheme.Auto));
    }

    private void ApplyCurrentTheme(AppTheme value)
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

    partial void OnAvatarUrlChanged(string? value)
    {
        AvatarImage = string.IsNullOrWhiteSpace(value)
            ? null
            : new BitmapImage(new Uri(value, UriKind.Absolute));
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
        AvatarUrl = _authService.AvatarUrl;
        GatewayStatus = _authService.IsAuthenticated ? StatusConnected : StatusNotSignedIn;
    }

    private void RefreshHostCount()
    {
        HostCount = _mcpEngine.InstanceManager.GetInstances().Count;
    }

    private void RefreshHosts()
    {
        Hosts.Clear();

        var connectedPids = new HashSet<int>();
        var instances = _mcpEngine.InstanceManager.GetInstances();

        foreach (var instance in instances)
        {
            connectedPids.Add(instance.ProcessId);
            Hosts.Add(new HostModel
            {
                Host = instance.HostApp ?? StatusUnknown,
                Version = instance.VersionNumber,
                Pid = instance.ProcessId,
                Status = StatusConnected
            });
        }

        foreach (var pipe in InstanceManager.DiscoverHostPipes())
        {
            if (!TryParseHostPipe(pipe, out var host, out var version, out var pid))
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

    private static bool TryParseHostPipe(string pipeName, out string host, out string version, out int pid)
    {
        host = string.Empty;
        version = string.Empty;
        pid = 0;

        var parts = pipeName.Split(PipeSeparator);
        if (parts.Length < MinPipeParts || !int.TryParse(parts[^1], out pid))
            return false;

        host = parts[0];
        version = string.Join(PipeSeparator, parts.Skip(1).Take(parts.Length - 2));
        return true;
    }
}
