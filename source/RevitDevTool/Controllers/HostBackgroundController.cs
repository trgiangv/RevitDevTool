using System.IO;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Extensions.Hosting;
using RevitDevTool.Commands;
using RevitDevTool.Engine;
using RevitDevTool.Logging.Listeners;
using RevitDevTool.Settings;
using RevitDevTool.Theme;
using RevitDevTool.Utils;
using RevitDevTool.CodeExecute.Providers.Python;

namespace RevitDevTool.Controllers;

public sealed class HostBackgroundController(ISettingsService settingsService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        LoadSettings();
        CleanLogFolder();
        LoadTheme();
        ToggleHardwareRendering(settingsService);
        PythonInitializer.InitializeAsync().ConfigureAwait(true);
        StartEngineHostAsync().ConfigureAwait(true);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        SaveSettings();
        CleanLogFolder();
        Shutdown();
        EngineHost.Instance.Dispose();
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
        if (string.IsNullOrEmpty(logFolder) || !Directory.Exists(logFolder)) return;

        var logFiles = Directory.EnumerateFiles(logFolder, "*.log");
        var jsonFiles = Directory.EnumerateFiles(logFolder, "*.json");

        foreach (var file in logFiles.Concat(jsonFiles))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Ignore
            }
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
    
    private static Task StartEngineHostAsync()
    {
        var engine = EngineHost.Instance;
        engine.ExecuteJobHandler = JobController.ExecuteAsync;

        var version = Context.Application.VersionNumber;
        var pid = SettingsUtils.CurrentProcessId;
        return engine.StartAsync("revit", version, pid);
    }

    private static void Shutdown()
    {
        NotifyListener.TraceReceived -= TraceCommand.TraceReceivedHandler;
        if (TraceCommand.SharedViewModel is not null)
        {
            TraceCommand.SharedViewModel.IsStarted = false;
        }
        VisualizationController.Stop();
        PythonInitializer.Shutdown();
    }
}