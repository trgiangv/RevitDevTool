using RevitDevTool.ViewModel;

namespace RevitDevTool.View;

public partial class MemoryView
{
    public MemoryView(MemoryViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
