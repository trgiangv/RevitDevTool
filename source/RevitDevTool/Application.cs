using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using RevitDevTool.Commands;
using RevitDevTool.Services ;
using RevitDevTool.Theme ;

namespace RevitDevTool;

[UsedImplicitly]
public class Application : ExternalApplication
{
    public override void OnStartup()
    {
        ExternalEventController.Register();
        SettingsService.Instance.LoadSettings();
        ThemeWatcher.Instance.Initialize();
        AddButton(Application);
        AddDockable(Application);
    }
    
    public override void OnShutdown()
    {
        SettingsService.Instance.SaveSettings();
        VisualizationController.Stop();
    }

    private static void AddDockable(UIControlledApplication application)
    {
        TraceCommand.RegisterDockablePane(application);
    }
    
    private static void AddButton(UIControlledApplication application)
    {
        var panel = application.CreatePanel("External Tools");

        panel.AddPushButton<TraceCommand>("Trace Panel")
            .SetLargeImage("/RevitDevTool;component/Resources/Icons/TraceGeometry32_light.tiff")
            .SetLongDescription("Show/Hide Trace Panel");
    }
}