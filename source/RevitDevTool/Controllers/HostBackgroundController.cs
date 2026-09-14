using System.IO;
using Autodesk.Windows;
using DevTools.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.IronPython;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.UI;
using RevitDevTool.Execution.PyRevit;
using RevitDevTool.Settings;
using DevTools.UI.Theme;
using ZLogger;

namespace RevitDevTool.Controllers;

[UsedImplicitly]
public sealed class HostBackgroundController(
    IHostAppInfo hostAppInfo,
    IRevitSettingsService settingsService,
    PythonInitializer pythonInitializer,
    IronPythonDebugger ironPythonDebugger,
    IIronPythonBridge ironPythonBridge,
    ILogger<HostBackgroundController> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        HostUiHelper.Initialize(ComponentManager.ApplicationWindow, ComponentManager.Ribbon.Dispatcher);

        var hostApp = hostAppInfo.Host;
        NetworkService.Configure(hostApp);
        PythonEmbedded.Configure(hostApp);

        settingsService.LoadSettings();
        ThemeManager.Current.ApplySettingsTheme((AppTheme)settingsService.GeneralConfig.Theme);
        HostUiHelper.ToggleHardwareRendering(settingsService.GeneralConfig.UseHardwareRendering);
        await Task.WhenAll(
            pythonInitializer.InitializeAsync(),
            InitializeIronPythonAsync()).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        settingsService.SaveSettings();
        CleanLogFolder();
        await pythonInitializer.ShutdownAsync().ConfigureAwait(false);
        ironPythonDebugger.Shutdown();
    }

    private async Task InitializeIronPythonAsync()
    {
        if (PyRevitLibraryPaths.IsLoaded)
        {
            logger.ZLogInformation(
                $"IronPython debug uses pyRevit ScriptExecutor engine. Root={PyRevitLibraryPaths.InstallRoot}");
            var engine = PyRevitReflectionCache.Instance.EnsureIronPythonEngine(logger);
            await ironPythonDebugger.InitializeAsync(engine).ConfigureAwait(false);
            return;
        }

        logger.ZLogInformation($"IronPython debug uses embedded 3.4.2.");
        await ironPythonDebugger.InitializeAsync(ironPythonBridge).ConfigureAwait(false);
    }

    private void CleanLogFolder()
    {
        var fileConfig = settingsService.LogConfig.FileLogging;
        if (!fileConfig.Enabled || !fileConfig.AutoClean) return;

        var logFolder = fileConfig.LogFolder;
        if (string.IsNullOrEmpty(logFolder) || !Directory.Exists(logFolder)) return;

        var logFiles = Directory.EnumerateFiles(logFolder, "log_*.log");
        var jsonFiles = Directory.EnumerateFiles(logFolder, "log_*.json");

        foreach (var file in logFiles.Concat(jsonFiles))
        {
            try { File.Delete(file); }
            catch { /* ignore */ }
        }
    }
}
