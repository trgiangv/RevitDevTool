using RevitDevTool.ViewModel.Settings;
namespace RevitDevTool.View;

public partial class SettingsView
{
    public SettingsView()
    {
        InitializeComponent();
        DataContext = Host.GetService<SettingsViewModel>();
    }
}
