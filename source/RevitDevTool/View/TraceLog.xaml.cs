using RevitDevTool.Theme;
using RevitDevTool.ViewModel;

namespace RevitDevTool.View;

public partial class TraceLog
{
    public TraceLog()
    {
        ThemeWatcher.Instance.Watch(this);
        InitializeComponent();
        DataContext = new TraceLogViewModel();
        Loaded += (_, _) =>
        {
            if (DataContext is TraceLogViewModel vm)
                vm.RefreshTheme();
        };
    }
}