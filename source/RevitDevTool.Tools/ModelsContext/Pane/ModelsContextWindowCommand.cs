using Autodesk.Revit.Attributes;
using DevTools.ModelsContext.Panel;
using DevTools.Settings;
using DevTools.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RevitDevTool.Core;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using ZLogger;
using ZLogger.Providers;

namespace RevitDevTool.Tools.ModelsContext.Pane;

[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public sealed class ModelsContextWindowCommand : IExternalCommand
{
    public const string WindowId = "ModelContext.Window";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            _ = RevitContextExecutor.Turn;
            CloseEarlierWindows();
            ModelsContextPane.Hide();
            Open(commandData.Application);
            return Result.Succeeded;
        }
        catch (Exception exception)
        {
            message = $"Could not open the Model Context window: {exception.Message}";
            return Result.Failed;
        }
    }

    private static void CloseEarlierWindows()
    {
        var windows = PresentationSource.CurrentSources
            .OfType<HwndSource>()
            .Select(source => source.RootVisual)
            .OfType<Window>()
            .Where(window => AutomationProperties.GetAutomationId(window) == WindowId)
            .ToList();
        foreach (var window in windows)
        {
            window.Close();
        }
    }

    private static void Open(UIApplication application)
    {
        var contentRoot = AppUtils.GetContentRootPath(application.Application.VersionNumber);
        var logs = Directory.CreateDirectory(Path.Combine(contentRoot, "Logs")).FullName;
        var pid = Environment.ProcessId;
        var provider = new ServiceCollection()
            .AddSettingServices(contentRoot)
            .AddLogging(builder => builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddZLoggerRollingFile(options =>
                {
                    options.FilePathSelector = (time, sequence) =>
                        Path.Combine(logs, $"models-context-window_{pid}_{time:yyyyMMdd}_{sequence:D3}.log");
                    options.RollingInterval = RollingInterval.Day;
                    options.FileShared = true;
                    options.UsePlainTextFormatter();
                }))
            .AddModelsContext()
            .BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<ModelsContextPane>>();
        var panel = provider.GetRequiredService<ModelsContextPanel>();
        logger.LogInformation("Model Context window opened.");
        try
        {
            var window = new Window
            {
                Title = ModelContextViewModel.PanelTitle,
                Width = 360,
                Height = 800,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = panel.Build()
            };
            AutomationProperties.SetAutomationId(window, WindowId);
            _ = new WindowInteropHelper(window) { Owner = application.MainWindowHandle };
            window.Closed += (_, _) => _ = CloseAsync(panel, provider, logger);
            panel.Start(RevitContext.UiControlledApplication);
            panel.IsActive = true;
            window.Show();
        }
        catch
        {
            panel.Stop();
            provider.Dispose();
            throw;
        }
    }

    private static async Task CloseAsync(ModelsContextPanel panel, ServiceProvider provider, ILogger logger)
    {
        try
        {
            await ModelsContextPanel.DropFiltersAsync(() => false);
            await RevitProfiler.RunAsync("Close window", _ => panel.Stop(), CancellationToken.None);
            logger.LogInformation("Model Context window closed.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Model Context window close failed.");
        }
        finally
        {
            provider.Dispose();
        }
    }
}
