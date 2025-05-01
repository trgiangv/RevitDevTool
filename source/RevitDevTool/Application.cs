using Nice3point.Revit.Toolkit.External;
using RevitDevTool.Revit.Command;
using RevitDevTool.Theme;
using RevitDevTool.Utils;
using RevitDevTool.View;

namespace RevitDevTool;

[UsedImplicitly]
public class Application : ExternalApplication
{
    public const string Name = "RevitDevTool";
    public const string Panel = "TraceLog";
    
    public override void OnStartup()
    {
        AddButton();
        AddDockable();
    }

    private void AddButton()
    {
        var panel = Application.CreatePanel(Panel, Name);

        panel.AddPushButton<TraceCommand>("Trace\nPanel")
            .SetLargeImage("/RevitDevTool;component/Images/log.png")
            .SetLongDescription("Display trace data");
        panel.AddPushButton<TraceGeometryCommand>("Trace\nGeometry")
            .SetLargeImage("/RevitDevTool;component/Images/switch-off.png")
            .SetLongDescription("Trace geometries data");
        panel.AddPushButton<ClearTraceGeometryCommand>("Clear\nGeometry")
            .SetLargeImage("/RevitDevTool;component/Images/eraser.png")
            .SetLongDescription("Clear current document trace geometries data");
    }

    private void AddDockable()
    {
        DockablePaneRegisterUtils.Register<TraceLog>(Resource.TraceGuid, Application);
    }
}