using Microsoft.Extensions.Logging;
using RevitDevTool.Models.Trace;
using RevitDevTool.Services;
using RevitDevTool.Utils;
using System.Diagnostics;

namespace RevitDevTool.Logging;

/// <summary>
/// Core logging service implementation.
/// Manages the complete logging lifecycle including initialization, trace listeners, and output.
/// </summary>
internal sealed class LoggingService(
    ISettingsService settingsService,
    ILoggerFactory loggerFactory,
    ITraceListenerFactory traceListenerFactory,
    ILogOutputSink outputSink) : ILoggingService
{
    private ILoggerAdapter? _logger;
    private GeometryListener? _geometryListener;
    private NotifyListener? _notifyListener;
    private bool _disposed;

    public ILoggerAdapter Logger => _logger ?? throw new InvalidOperationException("Logger not initialized. Call Initialize first.");
    public ILogOutputSink? OutputSink { get; } = outputSink;
    public TraceListener? TraceListener { get; private set; }

    public void Initialize(bool isDarkTheme)
    {
        var config = settingsService.LogConfig;

        OutputSink?.SetTheme(isDarkTheme);
        _logger = loggerFactory.CreateLogger(config, OutputSink, isDarkTheme);
        TraceListener = traceListenerFactory.CreateTraceListener(_logger, config);

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
            settingsService.LogConfig.IncludeStackTrace,
            TraceListener, _geometryListener, _notifyListener);
    }

    public void UnregisterTraceListeners()
    {
        TraceUtils.UnregisterTraceListeners(
            settingsService.LogConfig.IncludeStackTrace,
            TraceListener, _geometryListener, _notifyListener);
    }

    public void ClearOutput()
    {
        if (settingsService.LogConfig.UseExternalFileOnly) return;
        OutputSink?.Clear();
    }

    private void DisposeLogger()
    {
        TraceListener?.Dispose();
        TraceListener = null;
        _logger?.Dispose();
        _logger = null;
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

