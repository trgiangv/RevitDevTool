using DevTools.Presentation.ViewModels.Settings;
namespace RevitDevTool.View;

public partial class SettingsView
{
    public SettingsView()
    {
        InitializeComponent();
        DataContext = Host.GetService<SettingsViewModel>();
    }
}
