using RevitDevTool.Theme ;
using RevitDevTool.ViewModel.Settings ;

namespace RevitDevTool.View.Settings;

public sealed partial class FaceVisualizationSettingsView
{
    public FaceVisualizationSettingsView()
    {
        ThemeWatcher.Instance.Watch(this);
        DataContext = FaceVisualizationViewModel.Instance ;
        InitializeComponent();
    }
}