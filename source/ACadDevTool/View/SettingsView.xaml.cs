using DevTools.Views.ViewModel.Settings;

namespace AcadDevTool.View;

public partial class SettingsView
{
    public SettingsView()
    {
        InitializeComponent();
        DataContext = Host.GetService<SettingsViewModel>();
    }
}
