using RevitDevTool.ViewModel.Settings;

namespace RevitDevTool.View.Settings;

public partial class GeneralSettingsView
{
    public GeneralSettingsView()
    {
        DataContext = Host.GetService<GeneralSettingsViewModel>();
        InitializeComponent();
    }
}
