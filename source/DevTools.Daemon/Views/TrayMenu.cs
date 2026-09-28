using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.Daemon.Desktop;
using HandyControl.Controls;

namespace DevTools.Daemon.Views;

public partial class TrayMenu : ObservableObject
{
    private const string DefaultStatusText = "DevTools Daemon";
    private const string MenuResourceKey = "TrayMenu";

    private readonly AppState _state;
    private readonly MainWindow _window;
    private NotifyIcon? _notify;

    [ObservableProperty]
    public partial string StatusText { get; set; } = DefaultStatusText;

    public TrayMenu(AppState state, MainWindow window)
    {
        _state = state;
        _window = window;
        ThemeHelper.Changed += ApplyIcon;
        _state.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not (nameof(AppState.IsAuthenticated) or null)) return;
            SignInCommand.NotifyCanExecuteChanged();
            SignOutCommand.NotifyCanExecuteChanged();
        };
    }

    /// <summary>
    /// Registers the shell icon. <see cref="NotifyIcon"/> only does that from
    /// <see cref="NotifyIcon.Init"/>, and <c>Loaded</c> never runs while the
    /// icon stays off the visual tree (the dashboard starts hidden).
    /// </summary>
    public void Start()
    {
        var menu = (ContextMenu)Application.Current.FindResource(MenuResourceKey)!;
        _notify = new NotifyIcon
        {
            DataContext = this,
            Text = StatusText,
            ContextMenu = menu,
            Icon = AppIcons.ForTheme(ThemeHelper.IsLight(_state.Preferences.Theme)),
        };
        _notify.Click += (_, _) => ShowMainWindow();
        _notify.Init();
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
    private async Task SignIn() => await _state.SignInCommand.ExecuteAsync(null).ConfigureAwait(true);

    private bool CanSignIn() => !_state.IsAuthenticated;

    [RelayCommand(CanExecute = nameof(CanSignOut))]
    private async Task SignOut() => await _state.SignOut().ConfigureAwait(true);

    private bool CanSignOut() => _state.IsAuthenticated;

    [RelayCommand]
    private static void ExitApplication() => Application.Current.Shutdown();

    partial void OnStatusTextChanged(string value)
    {
        _notify?.Text = value;
    }

    private void ApplyIcon()
    {
        _notify?.Icon = AppIcons.ForTheme(ThemeHelper.IsLight(_state.Preferences.Theme));
    }
}
