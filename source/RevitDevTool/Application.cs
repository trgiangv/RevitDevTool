using DevTools.Utilities;
using Nice3point.Revit.Extensions.UI;
using Autodesk.Revit.DB.Events;
using RevitDevTool.Commands;
using RevitDevTool.Controllers;

namespace RevitDevTool;

[UsedImplicitly]
public class Application : IExternalApplication
{
    private UIControlledApplication? _uiControlledApplication;
    private bool _dockablePaneRegistered;

    public Result OnStartup(UIControlledApplication application)
    {
        _uiControlledApplication = application;
        AssemblyLoader.Initialize();
        ExternalEventController.Register();
        Host.Start();
        AddButton(application);
        HookDockablePaneInitialization(application);
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        UnhookDockablePaneInitialization(application);
        Host.Stop();
        return Result.Succeeded;
    }

    private static void HookDockablePaneInitialization(UIControlledApplication application)
    {
        application.ControlledApplication.DocumentOpening += OnDocumentOpening;
        application.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;
    }

    private static void UnhookDockablePaneInitialization(UIControlledApplication application)
    {
        application.ControlledApplication.DocumentOpening -= OnDocumentOpening;
        application.ControlledApplication.ApplicationInitialized -= OnApplicationInitialized;
    }

    private static void OnDocumentOpening(object? sender, DocumentOpeningEventArgs e)
    {
        AddDockablePane();
    }

    private static void OnApplicationInitialized(object? sender, ApplicationInitializedEventArgs e)
    {
        AddDockablePane();
    }

    private static void AddDockablePane()
    {
        if (Current is not { _dockablePaneRegistered: false, _uiControlledApplication: not null } app)
            return;

        DevToolsCommand.RegisterDockablePane(app._uiControlledApplication);
        app._dockablePaneRegistered = true;
    }

    internal static bool EnsureDockablePaneRegistered()
    {
        AddDockablePane();
        return Current?._dockablePaneRegistered == true;
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

    private static Application? Current { get; set; }

    public Application()
    {
        Current = this;
    }
}
