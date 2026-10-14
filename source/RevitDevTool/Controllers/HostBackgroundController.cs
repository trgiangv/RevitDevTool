using System.IO;
using Autodesk.Windows;
using DevTools.Execution.Providers;
using DevTools.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DevTools.Execution.Providers.IronPython;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.Mcp;
using DevTools.UI;
using RevitDevTool.Settings;
using DevTools.UI.Theme;
using ZLogger;

namespace RevitDevTool.Controllers;

[UsedImplicitly]
public sealed class HostBackgroundController(
    IHostAppInfo hostAppInfo,
    IRevitSettingsService settingsService,
    PythonInitializer pythonInitializer,
    IronPythonInitializer ironPythonInitializer,
    McpCatalogStore catalogStore,
    ILogger<HostBackgroundController> logger) : IHostedService
{
    private Task? _runtimeInit;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        HostUiHelper.Initialize(ComponentManager.ApplicationWindow, ComponentManager.Ribbon.Dispatcher);

        var hostApp = hostAppInfo.Host;
        NetworkService.Configure(hostApp);
        PythonEmbedded.Configure(hostApp);

        settingsService.LoadSettings();
        ThemeManager.Current.ApplySettingsTheme((AppTheme)settingsService.GeneralConfig.Theme);
        pythonInitializer.ReserveDebugPort();
        ironPythonInitializer.ReserveDebugPort();
        _runtimeInit = InitializeRuntimesAsync();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_runtimeInit is not null)
            await _runtimeInit.ConfigureAwait(false);

        settingsService.SaveSettings();
        CleanLogFolder();
        await pythonInitializer.ShutdownAsync().ConfigureAwait(false);
        await ironPythonInitializer.ShutdownAsync().ConfigureAwait(false);
    }

    private async Task InitializeRuntimesAsync()
    {
        try
        {
            await Task.WhenAll(
                pythonInitializer.InitializeAsync(),
                ironPythonInitializer.InitializeAsync()
            ).ConfigureAwait(false);

            if (pythonInitializer.IsInitialized)
            {
#if DEBUG
                logger.ZLogInformation($"Reloading MCP catalog after runtime init");
#endif
                await catalogStore.ReloadAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError($"Runtime init failed: {ex.Message}\n{ex.StackTrace}");
        }
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
