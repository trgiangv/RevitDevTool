using RevitDevTool.Theme ;
using RevitDevTool.ViewModel.Settings ;

namespace RevitDevTool.View.Settings;

public sealed partial class SolidVisualizationSettingsView
{
    public SolidVisualizationSettingsView()
    {
        ThemeWatcher.Instance.Watch(this);
        DataContext = SolidVisualizationViewModel.Instance;
        InitializeComponent();
    }
}