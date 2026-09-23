using System.ComponentModel;
using System.Windows;
using DevTools.Daemon.Desktop;

namespace DevTools.Daemon.Views;

public partial class MainWindow : Window
{
    private readonly AppState _state;

    public MainWindow(AppState state)
    {
        _state = state;
        InitializeComponent();
        DataContext = state;
        Closing += OnClosing;
        ThemeHelper.Changed += ApplyWindowIcon;
        ApplyWindowIcon();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void ApplyWindowIcon() =>
        Icon = AppIcons.ForTheme(ThemeHelper.IsLight(_state.Preferences.Theme));
}
