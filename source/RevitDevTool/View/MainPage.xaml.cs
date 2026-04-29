using DevTools.Presentation.ViewModels;

namespace RevitDevTool.View;

public partial class MainPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
