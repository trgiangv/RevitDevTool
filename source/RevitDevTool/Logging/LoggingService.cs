using Microsoft.Extensions.Logging;
using RevitDevTool.Bridge.IPC;
using RevitDevTool.Engine;
using RevitDevTool.Logging.Listeners;
using RevitDevTool.Logging.Python;
using RevitDevTool.Logger.Contracts;
using RevitDevTool.Logger.Listeners;
using RevitDevTool.Logger.Transport;
using RevitDevTool.Settings;
using RevitDevTool.Theme;
using RevitDevTool.Utils;
using ILoggerFactory = RevitDevTool.Logger.Contracts.ILoggerFactory;

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
    private static bool IsDarkTheme => ThemeManager.Current.ActualApplicationTheme == AppTheme.Dark;

    private bool _disposed;

    private ILoggerAdapter? _logger;
    public ILogOutputSink? OutputSink { get; } = outputSink;

    private GeometryListener? _geometryListener;
    private NotifyListener? _notifyListener;
    private LoggerTraceListener? _loggerTraceListener;
    private PipeLogTraceListener? _pipeLogListener;

    public void Initialize()
    {
        if (_logger != null)
        {
            Restart();
            return;
        }

        var config = settingsService.LogConfig;

        _logger = loggerFactory.CreateLogger(config, OutputSink, IsDarkTheme);
        _loggerTraceListener = traceListenerFactory.CreateTraceListener(_logger, config);
        _geometryListener ??= new GeometryListener();
        _notifyListener ??= new NotifyListener();
        if (config.EnablePipeLogBridge)
        {
            var sink = new PipeLogSink(async (logEvent, ct) =>
            {
                var payload = new PipeLogEntry
                {
                    TimestampUtc = logEvent.TimestampUtc.ToString("O"),
                    Level = logEvent.Level,
                    Message = logEvent.Message,
                    Source = logEvent.Source,
                    Exception = logEvent.Exception
                };
                await EngineHost.Instance.PublishLogAsync(payload, ct).ConfigureAwait(false);
            });
            _pipeLogListener = new PipeLogTraceListener(sink, msg => LogLevelDetector.DetectLogLevel(msg, config.FilterKeywords).ToString());
        }
        PyTrace.Initialize(settingsService);
    }

    public void Restart()
    {
        UnregisterTraceListeners();
        DisposeLogger();
        Initialize();
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
            _loggerTraceListener, _geometryListener, _notifyListener, _pipeLogListener);
    }

    public void UnregisterTraceListeners()
    {
        TraceUtils.UnregisterTraceListeners(
            settingsService.LogConfig.IncludeWpfTrace,
            _loggerTraceListener, _geometryListener, _notifyListener, _pipeLogListener);
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
        _pipeLogListener?.Dispose();
        _pipeLogListener = null;
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