using System.ComponentModel;
using ControlzEx.Theming;
using DevTools.UI;
using DevTools.UI.Theme;
using DevTools.Utilities;
using Microsoft.Win32;

namespace DevTools.Daemon.Dashboard;

public partial class DashboardWindow
{
    private readonly DashboardViewModel? _vm;

    public DashboardWindow(DashboardViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = vm;
        Loaded += (_, _) => ApplyTheme(vm.Theme);
        SystemEvents.UserPreferenceChanged += OnSystemThemeChanged;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DashboardViewModel.Theme) && sender is DashboardViewModel vm)
            ApplyTheme(vm.Theme);
    }

    private void OnSystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;
        if (_vm?.Theme != AppTheme.Auto) return;

        Dispatcher.Invoke(() => ApplyTheme(AppTheme.Auto));
    }

    private void ApplyTheme(AppTheme theme)
    {
        var isDark = theme switch
        {
            AppTheme.Light => false,
            AppTheme.Auto => !WindowsThemeHelper.AppsUseLightTheme(),
            _ => true
        };

        this.SetTitleBarTheme(isDark);
        Icon = isDark ? AppIcons.Dark : AppIcons.Light;
    }
}
