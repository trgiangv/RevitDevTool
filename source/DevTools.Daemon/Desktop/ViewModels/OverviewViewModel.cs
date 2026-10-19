using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Desktop.Helpers;
using DevTools.Daemon.Tunnel;

namespace DevTools.Daemon.Desktop.ViewModels;

public partial class OverviewViewModel : ObservableObject
{
    private const string StatusUnknown = "Unknown";
    private const string StatusNotSignedIn = "Not signed in";
    private const string StatusConnected = "Connected";
    private const string StatusDisconnected = "Disconnected";
    private const string SignInFailedTitle = "Sign In Failed";
    private const string SignInFailedMessage = "Sign in failed.";

    private static readonly HttpClient AvatarHttp = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly IAuthService _authService;
    private readonly ITunnelStatusProvider _tunnelStatus;
    private bool _isSignInInProgress;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial bool IsAuthenticated { get; set; }

    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ImageSource? AvatarImage { get; set; }

    [ObservableProperty]
    public partial string TunnelStatus { get; set; } = StatusDisconnected;

    public OverviewViewModel(IAuthService authService, ITunnelStatusProvider tunnelStatus)
    {
        _authService = authService;
        _tunnelStatus = tunnelStatus;

        RefreshAuthState();
        _authService.StateChanged += (_, _) => UiDispatchHelper.Post(RefreshAuthState);
        _tunnelStatus.StatusChanged += (_, args) =>
            UiDispatchHelper.Post(() => RefreshTunnelStatus(args.Status));
    }

    [RelayCommand(CanExecute = nameof(CanSignIn), AllowConcurrentExecutions = true)]
    private async Task SignInAsync()
    {
        if (_isSignInInProgress)
        {
            await _authService.SignInAsync().ConfigureAwait(false);
            return;
        }

        _isSignInInProgress = true;
        try
        {
            var result = await _authService.SignInAsync().ConfigureAwait(false);
            if (!result.Success)
                SignInFailure(result.Error ?? SignInFailedMessage);
        }
        catch (Exception ex)
        {
            SignInFailure(ex.Message);
        }
        finally
        {
            UiDispatchHelper.Post(() => _isSignInInProgress = false);
        }
    }

    public async Task SignOutAsync()
    {
        await _authService.SignOutAsync().ConfigureAwait(true);
        RefreshAuthState();
    }

    private bool CanSignIn() => !IsAuthenticated;

    private void RefreshAuthState()
    {
        IsAuthenticated = _authService.IsAuthenticated;
        DisplayName = _authService.DisplayName ?? string.Empty;
        Email = _authService.Email ?? string.Empty;
        AvatarImage = CreateAvatar(_authService.AvatarUrl);

        if (!_authService.IsAuthenticated)
            TunnelStatus = StatusNotSignedIn;
        else
            RefreshTunnelStatus(_tunnelStatus.Status);
    }

    private static void SignInFailure(string message) => UiDispatchHelper.Post(() =>
        HandyControl.Controls.MessageBox.Show(
            message,
            SignInFailedTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Error));

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

    private void RefreshTunnelStatus(TunnelStatus status)
    {
        TunnelStatus = status switch
        {
            Tunnel.TunnelStatus.Connected => StatusConnected,
            Tunnel.TunnelStatus.Connecting => "Connecting...",
            Tunnel.TunnelStatus.Reconnecting => "Reconnecting...",
            Tunnel.TunnelStatus.Disconnected => StatusDisconnected,
            _ => StatusUnknown,
        };
    }
}
