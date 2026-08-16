using System.Windows;
using DevTools.Logging;
using DevTools.Logging.Abstractions;
using DevTools.Logging.Listeners;
using DevTools.Logging.Options;
using DevTools.Settings.Configs;
using DevTools.Telemetry;
using Microsoft.Extensions.Logging;
using RevitDevTool.Logging.Listeners;
using RevitDevTool.Settings;
using DevTools.UI.Theme;
using DevTools.Presentation.Interfaces;

namespace RevitDevTool.Logging;

[UsedImplicitly]
public sealed class LoggingService(
    IRevitSettingsService settingsService,
    ILoggerFactory loggerFactory,
    IFileLogTarget fileLogTarget,
    IHttpLogTarget httpLogTarget,
    IMonitorLogTarget monitor,
    IContextEnricher contextEnricher,
    LoggingConfiguration loggingConfiguration,
    ITelemetry telemetry) : ILoggingService
{
    private bool _disposed;

    private LoggerTraceListener? _loggerTraceListener;
    private GeometryListener? _geometryListener;
    private NotifyListener? _notifyListener;
    private ConsoleRedirector? _consoleRedirector;
    private IDisposable? _enricherScope;

    public FrameworkElement HostElement => monitor.HostElement;

    public void Initialize()
    {
        PushEnricherScope();

        var config = settingsService.LogConfig;

        loggingConfiguration.SetMinimumLevel(config.TraceListener.LogLevel);

        monitor.Enable(config.Monitor);
        SetTheme(ThemeManager.Current.ActualApplicationTheme == DevTools.UI.Theme.AppTheme.Dark);
        monitor.SetPrettyJson(config.Monitor.EnablePrettyJson);
        monitor.SetFilter(config.TraceListener.LogLevel);

        if (config.FileLogging.Enabled)
            fileLogTarget.Enable(config.FileLogging);

        if (config.HttpLogging.Enabled)
            httpLogTarget.Enable(config.HttpLogging);

        RecreateTraceListeners(config);
        _consoleRedirector = new ConsoleRedirector();
    }

    public void EnableTarget(LogSink sink)
    {
        var config = settingsService.LogConfig;

        switch (sink)
        {
            case LogSink.File:
                PushEnricherScope();
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

    public void DisableTarget(LogSink sink)
    {
        switch (sink)
        {
            case LogSink.File:
                fileLogTarget.Disable();
                break;
            case LogSink.Http:
                httpLogTarget.Disable();
                break;
            case LogSink.Monitor:
                monitor.Disable();
                break;
        }
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
            _loggerTraceListener, _geometryListener, _notifyListener);
    }

    public void UnregisterTraceListeners()
    {
        // Remove NotifyListener first
        // with Trace and would otherwise raise TraceReceived during teardown.
        TraceListenerHelper.UnregisterTraceListeners(_notifyListener, _geometryListener, _loggerTraceListener);
    }

    public void ClearOutput()
    {
        monitor.Clear();
    }

    private void RecreateTraceListeners(LogConfig config)
    {
        UnregisterTraceListeners();
        DisposeListeners();

        var logger = loggerFactory.CreateLogger("");
        _loggerTraceListener = new LoggerTraceListener(logger, config.TraceListener, telemetry.RecordLoggerTrace);

        _geometryListener ??= new GeometryListener();
        _notifyListener ??= new NotifyListener();
        RegisterTraceListeners();
    }

    private void PushEnricherScope()
    {
        _enricherScope?.Dispose();

        var properties = contextEnricher.GetStaticProperties();
        var dynamic = contextEnricher.GetDynamicProperties();
        if (dynamic != null)
        {
            foreach (var kvp in dynamic) properties[kvp.Key] = kvp.Value;
        }

        if (properties.Count > 0)
        {
            _enricherScope = loggerFactory.CreateLogger("").BeginScope(properties);
        }
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
        _consoleRedirector?.Dispose();
        _consoleRedirector = null;
        UnregisterTraceListeners();
        DisposeListeners();
        _enricherScope?.Dispose();
        fileLogTarget.Disable();
        httpLogTarget.Disable();
        monitor.Disable();
        monitor.Dispose();
        _disposed = true;
    }
}
