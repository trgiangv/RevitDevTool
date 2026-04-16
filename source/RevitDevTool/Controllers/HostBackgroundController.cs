using System.IO;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Extensions.Hosting;
using RevitDevTool.Core;
using RevitDevTool.Execution.Providers.Python;
using RevitDevTool.Settings;
using RevitDevTool.Theme;

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
        await pythonInitializer.InitializeAsync().ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        settingsService.SaveSettings();
        CleanLogFolder();
        await pythonInitializer.ShutdownAsync().ConfigureAwait(false);
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