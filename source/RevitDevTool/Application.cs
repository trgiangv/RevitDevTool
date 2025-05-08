using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using RevitDevTool.Commands;

namespace RevitDevTool;

[UsedImplicitly]
public class Application : ExternalApplication
{
    public override void OnStartup()
    {
        ExternalEventController.Register();
        AddButton(Application);
        AddDockable(Application);
        VisualizationServerController.Start();
    }
    
    public override void OnShutdown()
    {
        VisualizationServerController.Stop();
    }

    private static void AddDockable(UIControlledApplication application)
    {
        TraceCommand.RegisterDockablePane(application);
    }
    
    private static void AddButton(UIControlledApplication application)
    {
        var panel = application.CreatePanel("External Tools");

        panel.AddPushButton<TraceCommand>("Trace Panel")
            .SetLargeImage("/RevitDevTool;component/Resources/Icons/TraceCommand32_light.tiff")
            .SetLongDescription("Show/Hide Trace Panel");
    }
}