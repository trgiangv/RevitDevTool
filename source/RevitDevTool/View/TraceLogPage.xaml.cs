using RevitDevTool.Theme;
using RevitDevTool.ViewModel;

namespace RevitDevTool.View;

public partial class TraceLogPage
{
    internal TraceLogPage(TraceLogViewModel viewModel)
    {
        ThemeWatcher.Instance.Watch(this);
        InitializeComponent();
        DataContext = viewModel;
    }
}