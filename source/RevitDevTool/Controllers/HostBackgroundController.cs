using System.IO;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Extensions.Hosting;
using RevitDevTool.Commands;
using RevitDevTool.Logging.Listeners;
using RevitDevTool.Settings;
using RevitDevTool.Theme;
using RevitDevTool.Execution.Providers.Python;

namespace RevitDevTool.Controllers;

public sealed class HostBackgroundController(ISettingsService settingsService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        settingsService.LoadSettings();
        ThemeManager.Current.ApplySettingsTheme(settingsService.GeneralConfig.Theme);
        ToggleHardwareRendering(settingsService);
        PythonInitializer.InitializeAsync().ConfigureAwait(true);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        settingsService.SaveSettings();
        CleanLogFolder();
        NotifyListener.TraceReceived -= DevToolsCommand.TraceReceivedHandler;
        DevToolsCommand.SharedViewModel?.IsStarted = false;
        VisualizationController.Stop();
        PythonInitializer.Shutdown();
        return Task.CompletedTask;
    }

    private void CleanLogFolder()
    {
        var config = settingsService.LogConfig;
        if (!config.IsSaveLogEnabled || !config.AutoClean) return;

        var logFolder = config.LogFolder;
        if (string.IsNullOrEmpty(logFolder) || !Directory.Exists(logFolder)) return;

        var logFiles = Directory.EnumerateFiles(logFolder, "*.log");
        var jsonFiles = Directory.EnumerateFiles(logFolder, "*.json");

        foreach (var file in logFiles.Concat(jsonFiles))
        {
            try { File.Delete(file); }
            catch { /* ignore */ }
        }
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
}