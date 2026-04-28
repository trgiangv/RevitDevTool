using DevTools.Views.ViewModel;

namespace RevitDevTool.View;

public partial class MainPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
