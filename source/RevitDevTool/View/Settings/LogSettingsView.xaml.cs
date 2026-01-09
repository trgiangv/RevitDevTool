using RevitDevTool.Theme;
using RevitDevTool.ViewModel.Settings;

namespace RevitDevTool.View.Settings;

public partial class LogSettingsView
{
    public LogSettingsView()
    {
        DataContext = Host.GetService<LogSettingsViewModel>();
        InitializeComponent();
        Host.GetService<IThemeWatcherService>().Watch(this);
    }
}