using RevitDevTool.Logging.Listeners;
using RevitDevTool.Logging.Python;
using RevitDevTool.Settings;
using RevitDevTool.Theme;
using RevitDevTool.Utils;
using ILogger = Serilog.ILogger;
using Serilog.Events;

namespace RevitDevTool.Logging;

/// <summary>
/// Core logging service implementation.
/// Manages the complete logging lifecycle including initialization, trace listeners, and output.
/// </summary>
public sealed class LoggingService(
    ISettingsService settingsService,
    LoggerFactory loggerFactory,
    ILoggingMonitor monitor) : ILoggingService
{
    private static bool IsDarkTheme => ThemeManager.Current.ActualApplicationTheme == AppTheme.Dark;

    private bool _disposed;

    private ILogger? _logger;
    public ILoggingMonitor? Monitor { get; } = monitor;

    private GeometryListener? _geometryListener;
    private NotifyListener? _notifyListener;
    private LoggerTraceListener? _loggerTraceListener;

    public void Initialize()
    {
        if (_logger != null)
        {
            Restart();
            return;
        }

        var config = settingsService.LogConfig;

        _logger = loggerFactory.CreateLogger(config, Monitor, IsDarkTheme);
        _loggerTraceListener = new LoggerTraceListener(_logger, config);
        _geometryListener ??= new GeometryListener();
        _notifyListener ??= new NotifyListener();
        PyTrace.Initialize(settingsService);
    }

    public void Restart()
    {
        UnregisterTraceListeners();
        DisposeLogger();
        Initialize();
        RegisterTraceListeners();
    }

    public void SetMinimumLevel(LogEventLevel level)
    {
        loggerFactory.SetMinimumLevel(level);
    }

    public void RegisterTraceListeners()
    {
        TraceUtils.RegisterTraceListeners(
            settingsService.LogConfig.IncludeWpfTrace,
            _loggerTraceListener, _geometryListener, _notifyListener);
    }

    public void UnregisterTraceListeners()
    {
        TraceUtils.UnregisterTraceListeners(
            settingsService.LogConfig.IncludeWpfTrace,
            _loggerTraceListener, _geometryListener, _notifyListener);
    }

    public void ClearOutput()
    {
        if (settingsService.LogConfig.UseExternalFileOnly) return;
        Monitor?.Clear();
    }

    private void DisposeLogger()
    {
        (_logger as IDisposable)?.Dispose();
        _logger = null;
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
        DisposeLogger();
        Monitor?.Dispose();
        _disposed = true;
    }
}