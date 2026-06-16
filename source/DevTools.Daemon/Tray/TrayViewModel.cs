using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Dashboard;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Daemon.Tray;

public partial class TrayViewModel : ObservableObject
{
    private const string DefaultStatusText = "DevTools Daemon";
    private const string SignInFailedTitle = "Sign In Failed";
    private const string SignInFailedFallback = "Sign in failed.";

    private readonly IAuthService _authService;
    private readonly IServiceProvider _services;

    [ObservableProperty] private string _statusText = DefaultStatusText;
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private int _hostCount;
    [ObservableProperty] private ImageSource _trayIconSource = AppIcons.ForCurrentTheme();

    public TrayViewModel(IAuthService authService, IServiceProvider services)
    {
        _authService = authService;
        _services = services;
        IsAuthenticated = authService.IsAuthenticated;
        authService.StateChanged += (_, args) => IsAuthenticated = args.IsAuthenticated;

        Microsoft.Win32.SystemEvents.UserPreferenceChanged += (_, _) =>
            Application.Current?.Dispatcher.Invoke(() => TrayIconSource = AppIcons.ForCurrentTheme());
    }

    [RelayCommand]
    private void OpenDashboard()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var window = Application.Current.MainWindow;
            if (window is not DashboardWindow)
            {
                window = _services.GetRequiredService<DashboardWindow>();
                Application.Current.MainWindow = window;
            }

            window.Show();
            window.Activate();
        });
    }

    [RelayCommand]
    private async Task SignIn()
    {
        var result = await _authService.SignInAsync();
        if (!result.Success)
            MessageBox.Show(result.Error ?? SignInFailedFallback, SignInFailedTitle);
    }

    [RelayCommand]
    private async Task SignOut()
    {
        await _authService.SignOutAsync();
    }

    [RelayCommand]
    private static void ExitApplication()
    {
        Application.Current.Shutdown();
    }
}
