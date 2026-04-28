using DevTools.Views.ViewModel.Settings;

namespace DevTools.Views.View.Settings;

public partial class GeneralSettingsView
{
    public GeneralSettingsView()
    {
        DataContext = ViewServiceLocator.GetRequired<GeneralSettingsViewModel>();
        InitializeComponent();
    }
}
