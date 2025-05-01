using Autodesk.Revit.UI;

namespace RevitDevTool.View;

public partial class TraceLog : IDockablePaneProvider
{
    public TraceLog()
    {
        InitializeComponent();
    }

    public void SetupDockablePane(DockablePaneProviderData data)
    {
        data.FrameworkElement = this;

        data.InitialState = new DockablePaneState
        {
            DockPosition = DockPosition.Right,
        };

        data.FrameworkElement.MinWidth = 320;
    }
}