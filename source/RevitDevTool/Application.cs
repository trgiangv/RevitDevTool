using Nice3point.Revit.Extensions;
using RevitDevTool.Commands;
using RevitDevTool.Controllers;
using RevitDevTool.Utils;

namespace RevitDevTool;

[UsedImplicitly]
public class Application : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        AssemblyLoader.Initialize();
        ExternalEventController.Register();
        Host.Start();
        AddButton(application);
        AddDockable(application);
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        Host.Stop();
        return Result.Succeeded;
    }

    private static void AddDockable(UIControlledApplication application)
    {
        DevToolsCommand.RegisterDockablePane(application);
    }

    private static void AddButton(UIControlledApplication application)
    {
        var panel = application.CreatePanel("External Tools");

        panel.AddPushButton<DevToolsCommand>(DevToolsCommand.CommandName)
            .AddShortcuts("AD")
            .SetAvailabilityController<DevToolsCommand>()
            .SetLargeImage("/RevitDevTool;component/Resources/Icons/TraceGeometry32_light.tiff")
            .SetToolTip("Execute last command\nCtrl + click to Show/Hide DevTools");

        panel.AddPushButton<StubBuilderCommand>("StubBuilder")
            .SetAvailabilityController<StubBuilderCommand>()
            .SetLargeImage("/RevitDevTool;component/Resources/Icons/python32.png")
            .SetToolTip("Generate Python .pyi stub files from .NET assemblies");
    }
}
