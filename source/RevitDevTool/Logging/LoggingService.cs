using Microsoft.Extensions.Logging;
using RevitDevTool.Listeners;
using RevitDevTool.Settings;
using RevitDevTool.Utils;

namespace RevitDevTool.Logging;

/// <summary>
/// Core logging service implementation.
/// Manages the complete logging lifecycle including initialization, trace listeners, and output.
/// </summary>
public sealed class LoggingService(
    ISettingsService settingsService,
    ILoggerFactory loggerFactory,
    ITraceListenerFactory traceListenerFactory,
    ILogOutputSink outputSink) : ILoggingService
{
    private bool _disposed;

    private ILoggerAdapter? _logger;
    public ILogOutputSink? OutputSink { get; } = outputSink;

    private GeometryListener? _geometryListener;
    private NotifyListener? _notifyListener;
    private LoggerTraceListener? _loggerTraceListener;

    public void Initialize(bool isDarkTheme)
    {
        if (_logger != null)
        {
            Restart(isDarkTheme);
            return;
        }

        var config = settingsService.LogConfig;

        OutputSink?.SetTheme(isDarkTheme);
        _logger = loggerFactory.CreateLogger(config, OutputSink, isDarkTheme);
        _loggerTraceListener = traceListenerFactory.CreateTraceListener(_logger, config);
        _geometryListener ??= new GeometryListener();
        _notifyListener ??= new NotifyListener();
    }

    public void Restart(bool isDarkTheme)
    {
        UnregisterTraceListeners();
        DisposeLogger();
        Initialize(isDarkTheme);
        RegisterTraceListeners();
    }

    public void SetMinimumLevel(LogLevel level)
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
        OutputSink?.Clear();
    }

    private void DisposeLogger()
    {
        _logger?.Dispose();
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
        OutputSink?.Dispose();
        _disposed = true;
    }
}

