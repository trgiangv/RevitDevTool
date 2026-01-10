using RevitDevTool.ViewModel;

namespace RevitDevTool.View;

public partial class TraceLogWindow
{
    public TraceLogWindow()
    {
        InitializeComponent();

        var pageViewModel = Host.GetService<TraceLogPageViewModel>();
        var traceLog = new TraceLogPage(pageViewModel);
        ContentFrame.Navigate(traceLog);
    }
}