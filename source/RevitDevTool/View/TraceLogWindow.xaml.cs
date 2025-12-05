using RevitDevTool.Theme;
using RevitDevTool.ViewModel;

namespace RevitDevTool.View;

public partial class TraceLogWindow
{
    internal TraceLogWindow(TraceLogViewModel viewModel)
    {
        ThemeWatcher.Instance.Watch(this);
        InitializeComponent();
        
        var traceLog = new TraceLogPage(viewModel);
        ContentFrame.Navigate(traceLog);
    }
}

