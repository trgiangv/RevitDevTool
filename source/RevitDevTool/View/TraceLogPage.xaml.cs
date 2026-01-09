using RevitDevTool.Theme;
using RevitDevTool.ViewModel;
using Wpf.Ui.Controls;

namespace RevitDevTool.View;

public partial class TraceLogPage
{
    public TraceLogPage(TraceLogPageViewModel viewModel)
    {
        Host.GetService<IThemeWatcherService>().Watch(this);
        InitializeComponent();
        DataContext = viewModel;
    }
}