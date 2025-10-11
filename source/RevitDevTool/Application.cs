using System.Reflection ;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using RevitDevTool.Commands;
using RevitDevTool.Services ;

namespace RevitDevTool;

[UsedImplicitly]
public class Application : ExternalApplication
{
    public override void OnStartup()
    {
        // AppDomain.CurrentDomain.AssemblyResolve += OnResolve;
        SettingsService.Instance.LoadSettings();
        Assembly.LoadFrom( @"C:\Users\truon\AppData\Roaming\Autodesk\Revit\Addins\2025\RevitDevTool\ColorPicker.Models.dll" ) ;
        Assembly.LoadFrom( @"C:\Users\truon\AppData\Roaming\Autodesk\Revit\Addins\2025\RevitDevTool\ColorPicker.dll" ) ;
        ExternalEventController.Register();
        AddButton(Application);
        AddDockable(Application);
    }
    
    public override void OnShutdown()
    {
        SettingsService.Instance.SaveSettings();
        AppDomain.CurrentDomain.AssemblyResolve -= OnResolve;
        VisualizationController.Stop();
    }

    private static void AddDockable(UIControlledApplication application)
    {
        TraceCommand.RegisterDockablePane(application);
    }
    
    private static Assembly? OnResolve(object? sender, ResolveEventArgs args)
    {
        var currentLocation = Assembly.GetExecutingAssembly().Location;
        var currentDirectory = System.IO.Path.GetDirectoryName(currentLocation);
        var assemblyName = new AssemblyName(args.Name).Name;
        var assemblyPath = System.IO.Path.Combine(currentDirectory ?? string.Empty, $"{assemblyName}.dll");
        return System.IO.File.Exists(assemblyPath) ? Assembly.LoadFrom(assemblyPath) : null ;
    }
    
    private static void AddButton(UIControlledApplication application)
    {
        var panel = application.CreatePanel("External Tools");

        panel.AddPushButton<TraceCommand>("Trace Panel")
            .SetLargeImage("/RevitDevTool;component/Resources/Icons/TraceGeometry32_light.tiff")
            .SetLongDescription("Show/Hide Trace Panel");
    }
}