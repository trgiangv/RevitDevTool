using System.Windows;
using DevTools.Logging;
using DevTools.Logging.Listeners;
using DevTools.Logging.Options;
using Microsoft.Extensions.Logging;
using RevitDevTool.Logging.Listeners;
using RevitDevTool.Settings;
using RevitDevTool.Theme;
using ZLogger.Scintilla.Public;

namespace RevitDevTool.Logging;

public sealed class LoggingService(
    ISettingsService settingsService,
    ILoggerFactory loggerFactory,
    IAppInfo appInfo,
    FileLogProcessor fileLogProcessor,
    LoggingConfiguration loggingConfiguration,
    ScintillaLogViewerWpf viewer) : ILoggingService
{
    private bool _disposed;

    private LoggerTraceListener? _loggerTraceListener;
    private GeometryListener? _geometryListener;
    private NotifyListener? _notifyListener;

    public FrameworkElement HostElement => viewer.HostElement as FrameworkElement ?? throw new InvalidOperationException("Viewer host element is not a FrameworkElement.");

    public void Initialize()
    {
        Restart();
        SetTheme(ThemeManager.Current.ActualApplicationTheme == AppTheme.Dark);
        viewer.Start();
    }

    public void Restart(LogTargets targets = LogTargets.All)
    {
        var config = settingsService.LogConfig;

        if (targets.HasFlag(LogTargets.File))
        {
            fileLogProcessor.Restart(config.FileLogging, appInfo);
        }

        if (targets.HasFlag(LogTargets.Monitor))
        {
            viewer.SetPrettyJson(config.Monitor.EnablePrettyJson);
            viewer.SetFilter(config.TraceListener.LogLevel);
        }

        if (targets != LogTargets.None)
        {
            RecreateTraceListeners(config);
        }
    }

    public void SetMinimumLevel(LogLevel level)
    {
        loggingConfiguration.SetMinimumLevel(level);
    }

    public void SetPrettyJson(bool enabled)
    {
        viewer.SetPrettyJson(enabled);
    }

    public void SetTheme(bool isDark)
    {
        viewer.SetTheme(isDark ? ScintillaThemes.Dark : ScintillaThemes.Light);
    }

    public void RegisterTraceListeners()
    {
        TraceListenerHelper.RegisterTraceListeners(
            settingsService.LogConfig.TraceListener.IncludeWpfTrace,
            _loggerTraceListener, _geometryListener, _notifyListener);
    }

    public void UnregisterTraceListeners()
    {
        TraceListenerHelper.UnregisterTraceListeners(
            settingsService.LogConfig.TraceListener.IncludeWpfTrace,
            _loggerTraceListener, _geometryListener, _notifyListener);
    }

    public void ClearOutput()
    {
        if (settingsService.LogConfig.Monitor.UseExternalFileOnly) return;
        viewer.Clear();
    }

    private void RecreateTraceListeners(LogConfig config)
    {
        UnregisterTraceListeners();
        DisposeListeners();

        var logger = loggerFactory.CreateLogger("");
        _loggerTraceListener = new LoggerTraceListener(logger, config.TraceListener);

        _geometryListener ??= new GeometryListener();
        _notifyListener ??= new NotifyListener();
        RegisterTraceListeners();
    }

    private void DisposeListeners()
    {
        _loggerTraceListener?.Dispose();
        _loggerTraceListener = null;
        _geometryListener?.Dispose();
        _geometryListener = null;
        _notifyListener?.Dispose();
        _notifyListener = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        UnregisterTraceListeners();
        DisposeListeners();
        fileLogProcessor.Stop();
        viewer.Stop();
        viewer.Dispose();
        _disposed = true;
    }
}
