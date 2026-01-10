using System.Windows.Interop;
using System.Windows.Media;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using RevitDevTool.Commands;
using RevitDevTool.Models.Trace;
using RevitDevTool.Services;
using RevitDevTool.Theme;

namespace RevitDevTool;

[UsedImplicitly]
public class Application : ExternalApplication
{
    public override void OnStartup()
    {
        Host.Start();
        ExternalEventController.Register();
        
        var settingsService = Host.GetService<ISettingsService>();
        settingsService.LoadSettings();
        
        // Initialize theme from settings
        ThemeManager.Current.ApplicationTheme = settingsService.GeneralConfig.Theme;
        
        EnableHardwareRendering();
        AddButton(Application);
        AddDockable(Application);
    }
    
    public override void OnShutdown()
    {
        NotifyListener.TraceReceived -= TraceCommand.TraceReceivedHandler;
        if (TraceCommand.SharedViewModel is not null) TraceCommand.SharedViewModel.IsStarted = false;
        Host.GetService<ISettingsService>().SaveSettings();
        VisualizationController.Stop();
        Host.Stop();
    }

    public static void EnableHardwareRendering()
    {
        var settingsService = Host.GetService<ISettingsService>();
        if (!settingsService.GeneralConfig.UseHardwareRendering) return;
        ExternalEventController.ActionEventHandler.Raise(_ => RenderOptions.ProcessRenderMode = RenderMode.Default);
    }

    public static void DisableHardwareRendering()
    {
        var settingsService = Host.GetService<ISettingsService>();
        if (settingsService.GeneralConfig.UseHardwareRendering) return;
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
