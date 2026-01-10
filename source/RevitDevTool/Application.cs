using System.Windows.Interop;
using System.Windows.Media;
using Nice3point.Revit.Toolkit.External;
using RevitDevTool.Commands;
using RevitDevTool.Controllers;
using RevitDevTool.Models.Trace;
using RevitDevTool.Services;
using RevitDevTool.Theme;
using RevitDevTool.Utils;

namespace RevitDevTool;

[UsedImplicitly]
public class Application : ExternalApplication
{
    public override void OnStartup()
    {
        AssemblyLoader.Initialize();
        Host.Start();
        ExternalEventController.Register();
        
        var settingsService = Host.GetService<ISettingsService>();
        settingsService.LoadSettings();
        ThemeManager.Current.ApplySettingsTheme(settingsService.GeneralConfig.Theme);
        
        EnableHardwareRendering();
        AddButton();
        AddDockable();
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
