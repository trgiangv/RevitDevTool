using RevitDevTool.Theme ;
using RevitDevTool.ViewModel.Settings ;

namespace RevitDevTool.View.Settings ;

public partial class GeneralSettingsView
{
    public GeneralSettingsView()
    {
        ThemeWatcher.Instance.Watch(this);
        DataContext = GeneralSettingsViewModel.Instance ;
        InitializeComponent() ;
    }
}