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
            .AddShortcuts("AD")
            .SetAvailabilityController<TraceCommand>()
            .SetLargeImage("/RevitDevTool;component/Resources/Icons/TraceGeometry32_light.tiff")
            .SetToolTip("Execute last command\nCtrl + click to Show/Hide Trace Panel");

        panel.AddPushButton<StubBuilderCommand>("StubBuilder")
            .SetAvailabilityController<StubBuilderCommand>()
            .SetLargeImage("/RevitDevTool;component/Resources/Icons/python32.png")
            .SetToolTip("Generate Python .pyi stub files from .NET assemblies");
    }
}
