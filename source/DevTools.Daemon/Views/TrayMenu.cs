using System.Drawing;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.Daemon.Desktop;
using H.NotifyIcon;

namespace DevTools.Daemon.Views;

public partial class TrayMenu : ObservableObject
{
    private const string DefaultStatusText = "DevTools Daemon";

    private readonly AppState _state;
    private readonly MainWindow _window;

    [ObservableProperty]
    public partial string StatusText { get; set; } = DefaultStatusText;

    public TrayMenu(AppState state, MainWindow window)
    {
        _state = state;
        _window = window;
        ApplyIcon();
        ThemeHelper.Changed += ApplyIcon;
        _state.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(AppState.IsAuthenticated) or null)
            {
                SignInCommand.NotifyCanExecuteChanged();
                SignOutCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public void ShowMainWindow()
    {
        UiDispatch.Send(() =>
        {
            Application.Current.MainWindow = _window;
            _window.Show();
            _window.Activate();
        });
    }

    [RelayCommand]
    private void OpenDashboard() => ShowMainWindow();

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignIn() => await _state.SignIn().ConfigureAwait(true);

    private bool CanSignIn() => !_state.IsAuthenticated;

    [RelayCommand(CanExecute = nameof(CanSignOut))]
    private async Task SignOut() => await _state.SignOut().ConfigureAwait(true);

    private bool CanSignOut() => _state.IsAuthenticated;

    [RelayCommand]
    private static void ExitApplication() => Application.Current.Shutdown();

    private void ApplyIcon()
    {
        if (Application.Current?.TryFindResource(AppConstants.TrayIconResourceKey) is not TaskbarIcon tray)
            return;

        var next = AppIcons.TrayIcon(ThemeHelper.IsLight(_state.Preferences.Theme));
        var previous = tray.Icon;
        tray.Icon = next;
        if (!ReferenceEquals(previous, next))
            previous?.Dispose();
    }
}
