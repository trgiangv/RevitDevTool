using System.ComponentModel;
using System.Windows;
using DevTools.Daemon.Desktop;
using DevTools.UI;

namespace DevTools.Daemon.Views;

public partial class MainWindow
{
    private readonly AppState _state;

    public MainWindow(AppState state)
    {
        _state = state;
        InitializeComponent();
        DataContext = state;
        Closing += OnClosing;
        Loaded += OnLoaded;
        Closed += OnClosed;
        ThemeHelper.Changed += OnThemeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        this.SetWindowButtons();
        ApplyChrome();
    }

    private void OnThemeChanged() => ApplyChrome();

    private void ApplyChrome()
    {
        var dark = !ThemeHelper.IsLight(_state.Preferences.Theme);
        Icon = AppIcons.ForTheme(!dark);
        this.SetTitleBarTheme(dark);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void OnClosed(object? sender, EventArgs e) =>
        ThemeHelper.Changed -= OnThemeChanged;
}
