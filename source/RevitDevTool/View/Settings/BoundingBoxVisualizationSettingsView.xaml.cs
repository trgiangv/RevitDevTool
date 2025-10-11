using RevitDevTool.Theme ;
using RevitDevTool.ViewModel.Settings ;

namespace RevitDevTool.View.Settings;

public sealed partial class BoundingBoxVisualizationSettingsView
{
    public BoundingBoxVisualizationSettingsView()
    {
        ThemeWatcher.Instance.Watch( this ) ;
        DataContext = BoundingBoxVisualizationViewModel.Instance;
        InitializeComponent();
    }
}