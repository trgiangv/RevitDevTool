using System.Reflection;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Gateway;
using DevTools.Mcp.Client;
using DevTools.Mcp.Core.Sessions;

namespace DevTools.Daemon.Desktop;

public sealed class AppState
{
    private const string StatusUnknown = "Unknown";
    private const string StatusNotSignedIn = "Not signed in";
    private const string StatusConnected = "Connected";
    private const string StatusDisconnected = "Disconnected";
    private const string DefaultVersion = "1.0.0";
    private const string SignInFailedTitle = "Sign In Failed";
    private const string SignInFailedMessage = "Sign in failed.";
    private static readonly HttpClient AvatarHttp = new();

    private readonly IAuthService _authService;
    private readonly ITunnelStatusProvider _tunnelStatus;

    public ObservableValue<int> SelectedTabIndex { get; } = new();
    public ObservableValue<bool> IsAuthenticated { get; } = new();
    public ObservableValue<string> DisplayName { get; } = new(string.Empty);
    public ObservableValue<string> Email { get; } = new(string.Empty);
    public ObservableValue<IImageSource?> AvatarImage { get; } = new();
    public ObservableValue<string> GatewayStatus { get; } = new(StatusDisconnected);

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

        SelectedTabIndex.Changed += OnTabChanged;
        RefreshAuthState();

        _authService.StateChanged += (_, _) => UiDispatch.Post(RefreshAuthState);
        _tunnelStatus.StatusChanged += (_, args) =>
            UiDispatch.Post(() => RefreshGatewayStatus(args.Status));
    }

    public async Task SignIn()
    {
        var result = await _authService.SignInAsync().ConfigureAwait(true);
        if (!result.Success)
        {
            await MessageBox.PromptAsync(new MessageBoxOptions
            {
                Message = result.Error ?? SignInFailedMessage,
                Title = SignInFailedTitle,
                Icon = PromptIconKind.Error,
                Owner = Application.Current.MainWindow,
            }).ConfigureAwait(true);
        }

        RefreshAuthState();
    }

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

    private void OnTabChanged()
    {
        if (SelectedTabIndex.Value == 1)
            Hosts.Refresh();
        else if (SelectedTabIndex.Value == 2)
            Preferences.ReloadAutoStart();
    }

    private void RefreshAuthState()
    {
        IsAuthenticated.Value = _authService.IsAuthenticated;
        DisplayName.Value = _authService.DisplayName ?? string.Empty;
        Email.Value = _authService.Email ?? string.Empty;
        LoadAvatar(_authService.AvatarUrl);

        if (!_authService.IsAuthenticated)
            GatewayStatus.Value = StatusNotSignedIn;
        else
            RefreshGatewayStatus(_tunnelStatus.Status);
    }

    private async void LoadAvatar(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            AvatarImage.Value = null;
            return;
        }

        try
        {
            var bytes = await AvatarHttp.GetByteArrayAsync(url).ConfigureAwait(true);
            AvatarImage.Value = ImageSource.FromBytes(bytes);
        }
        catch
        {
            AvatarImage.Value = null;
        }
    }

    private void RefreshGatewayStatus(TunnelStatus status)
    {
        GatewayStatus.Value = status switch
        {
            TunnelStatus.Connected => StatusConnected,
            TunnelStatus.Connecting => "Connecting...",
            TunnelStatus.Reconnecting => "Reconnecting...",
            TunnelStatus.Disconnected => StatusDisconnected,
            _ => StatusUnknown,
        };
    }
}
