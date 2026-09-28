using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Gateway;
using DevTools.Mcp.Client;
using DevTools.Mcp.Core.Sessions;

namespace DevTools.Daemon.Desktop;

public partial class AppState : ObservableObject
{
    private const string StatusUnknown = "Unknown";
    private const string StatusNotSignedIn = "Not signed in";
    private const string StatusConnected = "Connected";
    private const string StatusDisconnected = "Disconnected";
    private const string DefaultVersion = "1.0.0";
    private const string SignInFailedTitle = "Sign In Failed";
    private const string SignInFailedMessage = "Sign in failed.";

    private static readonly HttpClient AvatarHttp = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly IAuthService _authService;
    private readonly ITunnelStatusProvider _tunnelStatus;

    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    [ObservableProperty]
    public partial bool IsAuthenticated { get; set; }

    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ImageSource? AvatarImage { get; set; }

    [ObservableProperty]
    public partial string GatewayStatus { get; set; } = StatusDisconnected;

    public HostInstances Hosts { get; }
    public Preferences Preferences { get; }
    public string Version { get; }

    public AppState(
        IAuthService authService,
        IHostBroker hostBroker,
        IMcpPipeScanner pipeScanner,
        UserSettingsStore settings,
        ITunnelStatusProvider tunnelStatus)
    {
        _authService = authService;
        _tunnelStatus = tunnelStatus;
        Hosts = new HostInstances(hostBroker, pipeScanner);
        Preferences = new Preferences(settings);
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? DefaultVersion;

        RefreshAuthState();

        _authService.StateChanged += (_, _) => UiDispatch.Post(RefreshAuthState);
        _tunnelStatus.StatusChanged += (_, args) =>
            UiDispatch.Post(() => RefreshGatewayStatus(args.Status));
    }

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignIn()
    {
        var result = await _authService.SignInAsync().ConfigureAwait(true);
        if (!result.Success)
        {
            HandyControl.Controls.MessageBox.Show(
                result.Error ?? SignInFailedMessage,
                SignInFailedTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        RefreshAuthState();
    }

    private bool CanSignIn() => !IsAuthenticated;

    public async Task SignOut()
    {
        try
        {
            await _authService.SignOutAsync().ConfigureAwait(true);
        }
        catch
        {
            /* best-effort */
        }

        RefreshAuthState();
    }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnIsAuthenticatedChanged(bool value) =>
        SignInCommand.NotifyCanExecuteChanged();

    partial void OnSelectedTabIndexChanged(int value)
    {
        switch (value)
        {
            case 1:
                Hosts.Refresh();
                break;
            case 2:
                Preferences.ReloadAutoStart();
                break;
        }
    }

    private void RefreshAuthState()
    {
        IsAuthenticated = _authService.IsAuthenticated;
        DisplayName = _authService.DisplayName ?? string.Empty;
        Email = _authService.Email ?? string.Empty;
        AvatarImage = CreateAvatar(_authService.AvatarUrl);

        if (!_authService.IsAuthenticated)
            GatewayStatus = StatusNotSignedIn;
        else
            RefreshGatewayStatus(_tunnelStatus.Status);
    }

    internal static ImageSource? CreateAvatar(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            var bytes = AvatarHttp.GetByteArrayAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();
            return CreateAvatar(bytes);
        }
        catch
        {
            return null;
        }
    }

    internal static ImageSource? CreateAvatar(byte[] bytes)
    {
        if (bytes.Length == 0)
            return null;

        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = new MemoryStream(bytes);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
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
}
