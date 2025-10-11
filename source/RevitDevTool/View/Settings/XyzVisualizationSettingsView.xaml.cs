using RevitDevTool.Theme ;
using RevitDevTool.ViewModel.Settings ;

namespace RevitDevTool.View.Settings;

public sealed partial class XyzVisualizationSettingsView
{
    public XyzVisualizationSettingsView()
    {
        ThemeWatcher.Instance.Watch(this);
        DataContext = XyzVisualizationViewModel.Instance;
        InitializeComponent();
    }
}