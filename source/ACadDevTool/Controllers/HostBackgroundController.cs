using System.IO;
using AcadDevTool.Settings;
using Autodesk.Windows;
using DevTools.Hosting;
using DevTools.Execution.Providers.IronPython;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.Mcp.Catalog;
using DevTools.UI;
using DevTools.UI.Theme;
using Microsoft.Extensions.Hosting;

namespace AcadDevTool.Controllers;

public sealed class HostBackgroundController(
    IHostAppInfo hostAppInfo,
    IAcadSettingsService settingsService,
    PythonInitializer pythonInitializer,
    IronPythonInitializer ironPythonInitializer,
    McpCatalogStore catalogStore) : IHostedService
{
    private Task _runtimeInit = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        HostUiHelper.Initialize(ComponentManager.ApplicationWindow, ComponentManager.Ribbon.Dispatcher);

        var hostApp = hostAppInfo.Host;
        NetworkService.Configure(hostApp);
        PythonEmbedded.Configure(hostApp);

        settingsService.LoadSettings();
        ThemeManager.Current.ApplySettingsTheme((AppTheme)settingsService.GeneralConfig.Theme);
        HostUiHelper.ToggleHardwareRendering(settingsService.GeneralConfig.UseHardwareRendering);
        pythonInitializer.ReserveDebugPort();
        ironPythonInitializer.ReserveDebugPort();
        _runtimeInit = InitializeRuntimesAsync();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        settingsService.SaveSettings();
        CleanLogFolder();
        try
        {
            await _runtimeInit.ConfigureAwait(false);
        }
        catch
        {
            // InitializeAsync / pydevd already log failures.
        }

        await pythonInitializer.ShutdownAsync().ConfigureAwait(false);
        await ironPythonInitializer.ShutdownAsync().ConfigureAwait(false);
    }

    private async Task InitializeRuntimesAsync()
    {
        await Task.WhenAll(
            pythonInitializer.InitializeAsync(),
            ironPythonInitializer.InitializeAsync())
            .ConfigureAwait(false);

        try
        {
            await catalogStore.ReloadAsync().ConfigureAwait(false);
        }
        catch
        {
            // Built-in MCP tools remain; Python toolsets retry on the next ReloadAsync.
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
            catch { /* best-effort cleanup */ }
        }
    }
}
