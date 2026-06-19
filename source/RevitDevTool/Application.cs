using DevTools.Utilities;
using Nice3point.Revit.Extensions.UI;
using Autodesk.Revit.DB.Events;
using RevitDevTool.CommandBrowser;
using RevitDevTool.Commands;
using RevitDevTool.Controllers;

namespace RevitDevTool;

[UsedImplicitly]
public class Application : IExternalApplication
{
    private UIControlledApplication? _application;

    public Result OnStartup(UIControlledApplication application)
    {
        _application = application;
        AssemblyLoader.Initialize();
        Host.Start();
        AddButtons(application);
        application.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        Host.GetService<CommandBrowserController>().Shutdown();
        Host.GetService<PanelController>().Shutdown();
        Host.Stop();
        return Result.Succeeded;
    }

    private void OnApplicationInitialized(object? sender, ApplicationInitializedEventArgs e)
    {
        if (_application is null) return;
        Host.GetService<PanelController>().Initialize(_application);
        Host.GetService<CommandBrowserController>().Initialize(_application);
    }

    private static void AddButtons(UIControlledApplication application)
    {
        var panel = application.CreatePanel("External Tools");

        panel.AddPushButton<DevToolsCommand>(DevToolsCommand.CommandName)
            .AddShortcuts("AD")
            .SetAvailabilityController<DevToolsCommand>()
            .SetLargeImage("/DevTools.UI;component/Resources/Icons/DevTools-32-Light.png")
            .SetToolTip("Execute last command\nCtrl + click to Show/Hide DevTools");

        var stack = panel.AddStackPanel();

        stack.AddPushButton<StubBuilderCommand>("StubBuilder")
            .SetAvailabilityController<StubBuilderCommand>()
            .SetLargeImage("/DevTools.UI;component/Resources/Icons/StubBuilder-32-Light.png")
            .SetImage("/DevTools.UI;component/Resources/Icons/StubBuilder-16-Light.png")
            .SetToolTip("Generate Python .pyi stub files from .NET assemblies");

        stack.AddPushButton<CommandBrowserCommand>("Commands")
            .SetLargeImage("/DevTools.UI;component/Resources/Icons/Commands-32-Light.png")
            .SetImage("/DevTools.UI;component/Resources/Icons/Commands-16-Light.png")
            .SetToolTip("Search and run any Revit ribbon command");
    }
}
