using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.Daemon.Desktop.Helpers;
using DevTools.Daemon.Desktop.ViewModels;
using HandyControl.Controls;

namespace DevTools.Daemon.Desktop.Views;

public partial class TrayMenu : ObservableObject
{
    private const string DefaultStatusText = "DevTools Daemon";
    private const string MenuResourceKey = "TrayMenu";

    private readonly ShellViewModel _state;
    private readonly MainWindow _window;
    private NotifyIcon? _notify;

    [ObservableProperty]
    public partial string StatusText { get; set; } = DefaultStatusText;

    public TrayMenu(ShellViewModel state, MainWindow window)
    {
        _state = state;
        _window = window;
        ThemeHelper.Changed += ApplyIcon;
        _state.Overview.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not (nameof(OverviewViewModel.IsAuthenticated) or null)) return;
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
            Icon = AppIcons.ForTheme(ThemeHelper.IsLight(_state.Settings.Theme)),
        };
        _notify.Click += (_, _) => ShowMainWindow();
        _notify.Init();
    }

    public void ShowMainWindow()
    {
        UiDispatchHelper.Send(() =>
        {
            Application.Current.MainWindow = _window;
            _window.Show();
            _window.Activate();
        });
    }

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private void SignIn() => _state.Overview.SignInCommand.Execute(null);

    private bool CanSignIn() => !_state.Overview.IsAuthenticated;

    [RelayCommand(CanExecute = nameof(CanSignOut))]
    private async Task SignOut() => await _state.Overview.SignOutAsync().ConfigureAwait(true);

    private bool CanSignOut() => _state.Overview.IsAuthenticated;

    [RelayCommand]
    private static void ExitApplication() => Application.Current.Shutdown();

    partial void OnStatusTextChanged(string value)
    {
        _notify?.Text = value;
    }

    private void ApplyIcon()
    {
        _notify?.Icon = AppIcons.ForTheme(ThemeHelper.IsLight(_state.Settings.Theme));
    }
}
