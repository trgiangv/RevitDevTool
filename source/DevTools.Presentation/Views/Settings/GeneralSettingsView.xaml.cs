using DevTools.Presentation.ViewModels.Settings;
namespace DevTools.Presentation.Views.Settings;

public partial class GeneralSettingsView
{
    public GeneralSettingsView()
    {
        DataContext = ViewServiceLocator.GetRequired<GeneralSettingsViewModel>();
        InitializeComponent();
    }
}
