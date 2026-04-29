using DevTools.Presentation.ViewModels.Settings;

namespace AcadDevTool.View;

public partial class SettingsView
{
    public SettingsView()
    {
        InitializeComponent();
        DataContext = Host.GetService<SettingsViewModel>();
    }
}
