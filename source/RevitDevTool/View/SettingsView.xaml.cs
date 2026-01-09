using RevitDevTool.Theme;
namespace RevitDevTool.View;

public partial class SettingsView
{
    public SettingsView()
    {
        Host.GetService<IThemeWatcherService>().Watch(this);
        InitializeComponent();
    }
}