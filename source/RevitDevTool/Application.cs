using System.Reflection;
using System.Windows.Interop ;
using System.Windows.Media ;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using RevitDevTool.Commands;
using RevitDevTool.Models.Trace;
using RevitDevTool.Services ;
using RevitDevTool.Theme ;
using RevitDevTool.ViewModel;

namespace RevitDevTool;

[UsedImplicitly]
public class Application : ExternalApplication
{
    public override void OnStartup()
    {
        ExternalEventController.Register();
        SettingsService.Instance.LoadSettings();
        ThemeWatcher.Instance.Initialize();
        EnableHardwareRendering();
        AddButton(Application);
        AddDockable(Application);
    }
    
    public override void OnShutdown()
    {
        TraceEventNotifier.TraceReceived -= TraceCommand.TraceReceivedHandler;
        TraceCommand.SharedViewModel?.IsStarted = false;
        SettingsService.Instance.SaveSettings();
        VisualizationController.Stop();
    }

    public static void EnableHardwareRendering()
    {
        if (!SettingsService.Instance.GeneralConfig.UseHardwareRendering) return;
        ExternalEventController.ActionEventHandler.Raise(_ => RenderOptions.ProcessRenderMode = RenderMode.Default);
    }

    public static void DisableHardwareRendering()
    {
        if (SettingsService.Instance.GeneralConfig.UseHardwareRendering) return;
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
    }

    private static void AddDockable(UIControlledApplication application)
    {
        TraceCommand.RegisterDockablePane(application);
    }
    
    private static void AddButton(UIControlledApplication application)
    {
        var panel = application.CreatePanel("External Tools");

        panel.AddPushButton<TraceCommand>("Trace Panel")
            .SetAvailabilityController<CommandAvailability>()
            .SetLargeImage("/RevitDevTool;component/Resources/Icons/TraceGeometry32_light.tiff")
            .SetLongDescription("Show/Hide Trace Panel");
    }
}