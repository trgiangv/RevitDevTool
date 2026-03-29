using System.IO;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Extensions.Hosting;
using RevitDevTool.Commands;
using DevTools.Logging.Listeners;
using RevitDevTool.Settings;
using RevitDevTool.Theme;
using RevitDevTool.Execution.Providers.Python;

namespace RevitDevTool.Controllers;

public sealed class HostBackgroundController(
    ISettingsService settingsService,
    PythonInitializer pythonInitializer) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        settingsService.LoadSettings();
        ThemeManager.Current.ApplySettingsTheme(settingsService.GeneralConfig.Theme);
        ToggleHardwareRendering(settingsService);

        try
        {
            await pythonInitializer.InitializeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[Python] Background init failed {ex.Message}");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        settingsService.SaveSettings();
        CleanLogFolder();
        NotifyListener.TraceReceived -= DevToolsCommand.TraceReceivedHandler;
        DevToolsCommand.SharedViewModel?.IsStarted = false;
        VisualizationController.Stop();
        await pythonInitializer.Shutdown();
    }

    private void CleanLogFolder()
    {
        var fileConfig = settingsService.LogConfig.FileLogging;
        if (!fileConfig.Enabled || !fileConfig.AutoClean) return;

        var logFolder = fileConfig.LogFolder;
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
            ExternalEventController.ActionEventHandler.Raise(() => RenderOptions.ProcessRenderMode = RenderMode.Default);
        }
        else
        {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        }
    }
}