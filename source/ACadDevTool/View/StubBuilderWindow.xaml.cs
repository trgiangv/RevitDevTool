using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DevTools.UI.Theme;
using AcadDevTool.ViewModel;
using DevTools.Utilities;
namespace AcadDevTool.View;

public partial class StubBuilderWindow
{
    public StubBuilderWindow(StubBuilderViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        vm.CloseAction = () => DispatcherHelper.RunOnMainThread(Close);

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

    private void OnAssemblyItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: AssemblyItem item }) return;
        if (!File.Exists(item.Location)) return;
        Process.Start("explorer.exe", $"/select,\"{item.Location}\"");
    }
}
