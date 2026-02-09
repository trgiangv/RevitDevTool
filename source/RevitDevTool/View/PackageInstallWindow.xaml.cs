using RevitDevTool.Theme;
using RevitDevTool.Utils;
using RevitDevTool.ViewModel;
using System.Windows;

namespace RevitDevTool.View;

/// <summary>
/// Simple progress window for Python package installation.
/// </summary>
public partial class PackageInstallWindow
{
    public PackageInstallWindow(PackageInstallViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        
        vm.CloseAction = (result) => 
        {
            DispatcherHelper.RunOnMainThread(() => 
            {
                DialogResult = result;
                Close();
            });
        };

        Loaded += OnLoaded;
        ThemeManager.Current.ActualApplicationThemeChanged += OnThemeChanged;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        this.DisableWindowButtons(disableMinimize: true, disableMaximize: true, disableClose: true);
        ApplyTheme(ThemeManager.Current.ActualApplicationTheme);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        ApplyTheme(ThemeManager.Current.ActualApplicationTheme);
    }
    
    private void OnClosed(object? sender, EventArgs e)
    {
        ThemeManager.Current.ActualApplicationThemeChanged -= OnThemeChanged;
    }

    private void ApplyTheme(AppTheme theme)
    {
        this.SetImmersiveDarkMode(theme == AppTheme.Dark);
    }
}
