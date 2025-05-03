using System.IO;
using System.Reflection;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitDevTool.Commands;
using RevitDevTool.Extensions;
using RevitDevTool.Handlers;

namespace RevitDevTool;

[PublicAPI]
public class Application : IExternalApplication
{
    public static UIControlledApplication RevitUiControlledApplication { get; private set; } = null!;
    public static UIApplication RevitUiApplication => new RibbonItemEventArgs().Application;
    public static Autodesk.Revit.ApplicationServices.Application RevitApplication => RevitUiApplication.Application;
    public static UIDocument? RevitActiveUiDocument => RevitUiApplication.ActiveUIDocument;
    public static Document? RevitActiveDocument => RevitActiveUiDocument?.Document;
    public static bool IsInRevitApiMode => RevitUiApplication.ActiveAddInId is not null;

    public Result OnStartup(UIControlledApplication application)
    {
        ResolveAssemblies();
        RevitUiControlledApplication = application;
        ExternalEventController.Register();
        AddButton(application);
        AddDockable(application);
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        return Result.Succeeded;
    }
    
    private static void AddDockable(UIControlledApplication application)
    {
        TraceCommand.RegisterDockablePane(application);
    }
    
    private static void AddButton(UIControlledApplication application)
    {
        var panel = application.CreateRibbonPanel("DevTool");

        panel.AddPushButton<TraceCommand>("Trace\nPanel")
            .SetLargeImage("/RevitDevTool;component/Images/log.png")
            .SetLongDescription("Display trace data");
    }

    private static void ResolveAssemblies()
    {
        var currentAssemblyPath = Assembly.GetExecutingAssembly().Location;
        var currentAssemblyDirectory = Path.GetDirectoryName(currentAssemblyPath);
        if (currentAssemblyDirectory is null) return;
        var assemblyFiles = Directory.GetFiles(currentAssemblyDirectory, "*.dll");
        foreach (var assemblyFile in assemblyFiles)
        {
            Assembly.LoadFrom(assemblyFile);
        }
    }
}