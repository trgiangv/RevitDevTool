using System.ComponentModel;
using System.Windows;
using DevTools.Daemon.Desktop.Helpers;
using DevTools.Daemon.Desktop.ViewModels;
using DevTools.UI;

namespace DevTools.Daemon.Desktop.Views;

public partial class MainWindow
{
    private readonly ShellViewModel _state;

    public MainWindow(ShellViewModel state)
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
        var dark = !ThemeHelper.IsLight(_state.Settings.Theme);
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
