using System.IO;
using AcadDevTool.Settings;
using DevTools.Logging;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.Utilities;
using DevTools.UI.Theme;
using Microsoft.Extensions.Hosting;

namespace AcadDevTool.Controllers;

public sealed class HostBackgroundController(
    IHostAppInfo hostAppInfo,
    IAcadSettingsService settingsService,
    PythonInitializer pythonInitializer) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var hostApp = hostAppInfo.Host;
        NetworkService.Configure(hostApp);
        PythonEmbedded.Configure(hostApp);

        settingsService.LoadSettings();
        ThemeManager.Current.ApplySettingsTheme(settingsService.GeneralConfig.Theme);
        DispatcherHelper.ToggleHardwareRendering(settingsService.GeneralConfig.UseHardwareRendering);
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
            catch { /* best-effort cleanup */ }
        }
    }
}
