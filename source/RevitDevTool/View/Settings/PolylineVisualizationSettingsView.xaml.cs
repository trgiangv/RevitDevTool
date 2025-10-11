using RevitDevTool.Theme ;
using RevitDevTool.ViewModel.Settings ;

namespace RevitDevTool.View.Settings;

public sealed partial class PolylineVisualizationSettingsView
{
    public PolylineVisualizationSettingsView()
    {
        ThemeWatcher.Instance.Watch(this);
        DataContext = PolylineVisualizationViewModel.Instance;
        InitializeComponent();
    }
}