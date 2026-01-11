using Nice3point.Revit.Toolkit.External;
using RevitDevTool.Commands;
using RevitDevTool.Controllers;
using RevitDevTool.Utils;

namespace RevitDevTool;

[UsedImplicitly]
public class Application : ExternalApplication
{
    public override void OnStartup()
    {
        AssemblyLoader.Initialize();
        ExternalEventController.Register();
        Host.Start();
        AddButton();
        AddDockable();
    }
    
    public override void OnShutdown()
    {
        Host.Stop();
    }

    private void AddDockable()
    {
        TraceCommand.RegisterDockablePane(Application);
    }
    
    private void AddButton()
    {
        var panel = Application.CreatePanel("External Tools");

        panel.AddPushButton<TraceCommand>("Trace Panel")
            .SetAvailabilityController<TraceCommand>()
            .SetLargeImage("/RevitDevTool;component/Resources/Icons/TraceGeometry32_light.tiff")
            .SetLongDescription("Show/Hide Trace Panel");
    }
}
