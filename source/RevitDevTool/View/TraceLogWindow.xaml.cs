namespace RevitDevTool.View;

public partial class TraceLogWindow
{
    public TraceLogWindow(TraceLogPage traceLog)
    {
        InitializeComponent();
        ContentFrame.Navigate(traceLog);
    }
}