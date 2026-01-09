using RevitDevTool.Theme;
using RevitDevTool.ViewModel.Settings ;

namespace RevitDevTool.View.Settings ;

public partial class GeneralSettingsView
{
    public GeneralSettingsView()
    {
        Host.GetService<IThemeWatcherService>().Watch(this);
        DataContext = Host.GetService<GeneralSettingsViewModel>();
        InitializeComponent() ;
    }
}