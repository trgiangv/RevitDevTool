using DevTools.Utilities;
using RevitDevTool.Theme;
using System.Windows;

namespace RevitDevTool.View;

public partial class MainWindow
{
    public MainWindow(MainPage main)
    {
        InitializeComponent();
        ContentFrame.Navigate(main);

        Loaded += OnLoaded;
        ThemeManager.Current.ActualApplicationThemeChanged += OnThemeChanged;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        this.SetWindowButtons();
        ApplyTheme(ThemeManager.Current.ActualApplicationTheme);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        ApplyTheme(ThemeManager.Current.ActualApplicationTheme);
    }

    private void ApplyTheme(AppTheme theme)
    {
        var isDark = theme == AppTheme.Dark;
        this.SetTitleBarTheme(isDark);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        ThemeManager.Current.ActualApplicationThemeChanged -= OnThemeChanged;
    }
}