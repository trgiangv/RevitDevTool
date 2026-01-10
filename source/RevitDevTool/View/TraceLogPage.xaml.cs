using RevitDevTool.ViewModel;

namespace RevitDevTool.View;

public partial class TraceLogPage
{
    public TraceLogPage(TraceLogPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
