using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using DevTools.Daemon.Desktop;

namespace DevTools.Daemon.Views;

public sealed class MainWindow : Window
{
    private readonly AppState _state;

    public MainWindow(AppState state)
    {
        _state = state;
        this.Resizable(700, 500, minWidth: 500, minHeight: 400);
        StartupLocation = WindowStartupLocation.CenterScreen;
        Closing += OnClosing;
        ThemeHelper.Changed += ApplyWindowIcon;
        ApplyWindowIcon();
    }

    protected override void OnBuild()
    {
        this.Title("DevTools Daemon")
            .Content(
                new TabControl()
                    .Bind(TabControl.SelectedIndexProperty, _state.SelectedTabIndex)
                    .TabItems(
                        new TabItem().Header("Overview").Content(new OverviewView(_state)),
                        new TabItem().Header("Hosts").Content(new HostsView(_state.Hosts)),
                        new TabItem().Header("Settings").Content(new SettingsView(_state.Preferences, _state.Version))));
    }

    private void OnClosing(ClosingEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void ApplyWindowIcon() =>
        Icon = AppIcons.WindowIcon(ThemeHelper.IsLight(_state.Preferences.Theme.Value));
}
