using DevTools.Presentation.ViewModels;
namespace DevTools.Presentation.Views;

public partial class MemoryView
{
    public MemoryView(MemoryViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
