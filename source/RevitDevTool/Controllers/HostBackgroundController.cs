using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Extensions.Hosting;
using RevitDevTool.Commands;
using RevitDevTool.Models.Trace;
using RevitDevTool.Services;
using RevitDevTool.Theme;

namespace RevitDevTool.Controllers;

public sealed class HostBackgroundController(ISettingsService settingsService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        LoadSettings();
        LoadTheme();
        ToggleHardwareRendering(settingsService);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        SaveSettings();
        Shutdown();
        return Task.CompletedTask;
    }
    
    private void SaveSettings()
    {
        settingsService.SaveSettings();
    }

    private void LoadSettings()
    {
        settingsService.LoadSettings();
    }
    
    private void LoadTheme()
    {
        ThemeManager.Current.ApplySettingsTheme(settingsService.GeneralConfig.Theme);
    }

    public static void ToggleHardwareRendering(ISettingsService settingsService)
    {
        var useHardwareRendering = settingsService.GeneralConfig.UseHardwareRendering;
        if (useHardwareRendering)
        {
            ExternalEventController.ActionEventHandler.Raise(_ => RenderOptions.ProcessRenderMode = RenderMode.Default);
        }
        else
        {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        }
    }

    private static void Shutdown()
    {
        NotifyListener.TraceReceived -= TraceCommand.TraceReceivedHandler;
        if (TraceCommand.SharedViewModel is not null)
        {
            TraceCommand.SharedViewModel.IsStarted = false;
        }
        VisualizationController.Stop();
    }
}