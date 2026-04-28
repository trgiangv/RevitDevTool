using DevTools.Views.ViewModel;

namespace DevTools.Views.View;

public partial class ExecutionView
{
    public ExecutionView(ExecutionViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            viewModel.CommandViewModel.LoadSavedPathsAsync);
    }
}
