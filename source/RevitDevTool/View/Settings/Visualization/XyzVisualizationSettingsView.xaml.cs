using RevitDevTool.ViewModel.Settings.Visualization;

namespace RevitDevTool.View.Settings.Visualization;

public sealed partial class XyzVisualizationSettingsView
{
    public XyzVisualizationSettingsView(XyzVisualizationViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
