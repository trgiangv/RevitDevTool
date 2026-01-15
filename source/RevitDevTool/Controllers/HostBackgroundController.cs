using Microsoft.Extensions.Hosting;
using RevitDevTool.Commands;
using RevitDevTool.Listeners;
using RevitDevTool.Settings;
using RevitDevTool.Theme;
using System.Windows.Interop;
using System.Windows.Media;

namespace RevitDevTool.Controllers;

public sealed class HostBackgroundController(ISettingsService settingsService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        LoadSettings();
        CleanLogFolder();
        LoadTheme();
        ToggleHardwareRendering(settingsService);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        SaveSettings();
        CleanLogFolder();
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

    private void CleanLogFolder()
    {
        var config = settingsService.LogConfig;
        if (!config.IsSaveLogEnabled || !config.AutoClean) return;

        var logFolder = config.LogFolder;
        if (string.IsNullOrEmpty(logFolder) || !System.IO.Directory.Exists(logFolder)) return;

        try
        {
            var logFiles = System.IO.Directory.EnumerateFiles(logFolder, "*.log");
            var jsonFiles = System.IO.Directory.EnumerateFiles(logFolder, "*.json");

            foreach (var file in logFiles.Concat(jsonFiles))
            {
                try
                {
                    System.IO.File.Delete(file);
                }
                catch
                {
                    // Ignore files that cannot be deleted (e.g., in use)
                }
            }
        }
        catch
        {
            // Ignore errors when accessing the folder
        }
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