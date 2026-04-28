using DevTools.Views.ViewModel;

namespace AcadDevTool.View;

public partial class MainPage
{
    public MainPage()
    {
        InitializeComponent();
        DataContext = Host.GetService<MainViewModel>();
    }
}
