using DevTools.Presentation.ViewModels.Settings;
namespace DevTools.Presentation.Views.Settings;

public partial class LogSettingsView
{
    public LogSettingsView()
    {
        DataContext = ViewServiceLocator.GetRequired<LogSettingsViewModel>();
        InitializeComponent();
    }
}
