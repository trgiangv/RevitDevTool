using DevTools.Views.ViewModel;

namespace DevTools.Views.View;

public partial class MemoryView
{
    public MemoryView(MemoryViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
