using DevTools.Utilities;
using RevitDevTool.Theme;
using RevitDevTool.Utils;
using RevitDevTool.ViewModel;
using System.Windows;

namespace RevitDevTool.View;

public partial class StubBuilderWindow
{
    public StubBuilderWindow(StubBuilderViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        vm.CloseAction = () =>
        {
            DispatcherHelper.RunOnMainThread(Close);
        };

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
        this.SetTitleBarTheme(theme == AppTheme.Dark);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        ThemeManager.Current.ActualApplicationThemeChanged -= OnThemeChanged;
    }
}
