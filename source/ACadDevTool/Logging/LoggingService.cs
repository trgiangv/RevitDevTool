using System.Windows;
using AcadDevTool.Settings;
using DevTools.Logging;
using DevTools.Logging.Abstractions;
using DevTools.Logging.Listeners;
using DevTools.Logging.Options;
using DevTools.Settings.Configs;
using DevTools.UI.Theme;
using DevTools.Presentation.Interfaces;
using Microsoft.Extensions.Logging;

namespace AcadDevTool.Logging;

public sealed class LoggingService(
    IAcadSettingsService settingsService,
    ILoggerFactory loggerFactory,
    IFileLogTarget fileLogTarget,
    IHttpLogTarget httpLogTarget,
    IMonitorLogTarget monitor,
    LoggingConfiguration loggingConfiguration) : ILoggingService
{
    private bool _disposed;
    private LoggerTraceListener? _loggerTraceListener;

    public FrameworkElement HostElement => monitor.HostElement;

    public void Initialize()
    {
        var config = settingsService.LogConfig;

        monitor.Enable(config.Monitor);
        SetTheme(ThemeManager.Current.ActualApplicationTheme == AppTheme.Dark);
        monitor.SetPrettyJson(config.Monitor.EnablePrettyJson);
        monitor.SetFilter(config.TraceListener.LogLevel);

        if (config.FileLogging.Enabled)
            fileLogTarget.Enable(config.FileLogging);

        if (config.HttpLogging.Enabled)
            httpLogTarget.Enable(config.HttpLogging);

        RecreateTraceListeners(config);
    }

    public void EnableTarget(LogSink sink)
    {
        var config = settingsService.LogConfig;

        switch (sink)
        {
            case LogSink.File:
                fileLogTarget.Enable(config.FileLogging);
                break;
            case LogSink.Http:
                httpLogTarget.Enable(config.HttpLogging);
                break;
            case LogSink.Monitor:
                monitor.Enable(config.Monitor);
                monitor.SetPrettyJson(config.Monitor.EnablePrettyJson);
                monitor.SetFilter(config.TraceListener.LogLevel);
                break;
        }

        RecreateTraceListeners(config);
    }

    public void SetMinimumLevel(LogLevel level)
    {
        loggingConfiguration.SetMinimumLevel(level);
    }

    public void SetPrettyJson(bool enabled)
    {
        monitor.SetPrettyJson(enabled);
    }

    public void SetTheme(bool isDark)
    {
        monitor.SetTheme(isDark);
    }

    public void RegisterTraceListeners()
    {
        TraceListenerHelper.RegisterTraceListeners(
            settingsService.LogConfig.TraceListener.IncludeWpfTrace,
            _loggerTraceListener);
    }

    public void UnregisterTraceListeners()
    {
        TraceListenerHelper.UnregisterTraceListeners(
            settingsService.LogConfig.TraceListener.IncludeWpfTrace,
            _loggerTraceListener);
    }

    public void ClearOutput()
    {
        monitor.Clear();
    }

    private void RecreateTraceListeners(LogConfig config)
    {
        UnregisterTraceListeners();
        _loggerTraceListener?.Dispose();

        var logger = loggerFactory.CreateLogger("");
        _loggerTraceListener = new LoggerTraceListener(logger, config.TraceListener);
        RegisterTraceListeners();
    }

    public void Dispose()
    {
        if (_disposed) return;
        UnregisterTraceListeners();
        _loggerTraceListener?.Dispose();
        _loggerTraceListener = null;
        fileLogTarget.Disable();
        httpLogTarget.Disable();
        monitor.Disable();
        monitor.Dispose();
        _disposed = true;
    }
}
