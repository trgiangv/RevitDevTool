using RevitDevTool.Services ;
using RevitDevTool.Theme ;
using Wpf.Ui ;
using ApplicationTheme = UIFramework.ApplicationTheme;

namespace RevitDevTool.View ;

public partial class SettingsView
{
    public SettingsView()
    {
        ThemeWatcher.Instance.Watch(this);
        InitializeComponent() ;
        
#if REVIT2024_OR_GREATER
        ApplicationTheme.CurrentTheme.PropertyChanged += ThemeWatcher.ApplyTheme;
#endif
        var navigationService = CreateNavigationService() ;
        navigationService.SetNavigationControl(RootNavigation);
        
        FixComponentsTheme();
    }

    private static NavigationService CreateNavigationService()
    {
        var pageProvider = new SimplePageProvider();
        return new NavigationService(pageProvider);
    }
}