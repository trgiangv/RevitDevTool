using DevTools.Views.ViewModel.Settings;

namespace DevTools.Views.View.Settings;

public partial class LogSettingsView
{
    public LogSettingsView()
    {
        DataContext = ViewServiceLocator.GetRequired<LogSettingsViewModel>();
        InitializeComponent();
    }
}
