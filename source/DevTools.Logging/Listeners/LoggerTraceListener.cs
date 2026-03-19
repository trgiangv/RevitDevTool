using System.Diagnostics;
using DevTools.Logging.Options;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Logging.Listeners;

public class LoggerTraceListener : TraceListener
{
    private readonly ILogger _logger;
    private readonly TraceListenerOptions _options;

    private readonly string[] _criticalKeywords;
    private readonly string[] _errorKeywords;
    private readonly string[] _warningKeywords;
    private readonly string[] _informationKeywords;

    public LoggerTraceListener(ILogger logger, TraceListenerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _criticalKeywords = LogLevelDetector.ParseKeywords(options.FilterKeywords.Critical);
        _errorKeywords = LogLevelDetector.ParseKeywords(options.FilterKeywords.Error);
        _warningKeywords = LogLevelDetector.ParseKeywords(options.FilterKeywords.Warning);
        _informationKeywords = LogLevelDetector.ParseKeywords(options.FilterKeywords.Information);
    }

    public override bool IsThreadSafe => true;

    public override void Fail(string? message)
        => Log(LogLevel.Critical, null, $"{message}");

    public override void Fail(string? message, string? detailMessage)
        => Log(LogLevel.Critical, null, $"{message} | {detailMessage}");

    public override void TraceData(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, object? data)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, "", null, data, null)) return;
        LogWithStackTrace(GetLogLevel(eventType), null, $"{data}", eventCache);
    }

    public override void TraceData(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, params object?[]? data)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, "", null, null, data)) return;
        var message = data is { Length: > 0 } ? string.Join(", ", data) : "";
        LogWithStackTrace(GetLogLevel(eventType), null, message, eventCache);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, "", null, null, null)) return;
        LogWithStackTrace(GetLogLevel(eventType), null, $"{source} {eventType}: {id}", eventCache);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, message, null, null, null)) return;
        LogWithStackTrace(GetLogLevel(eventType), null, message, eventCache);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, format, args, null, null)) return;
        var level = GetLogLevel(eventType);
        LogWithStackTrace(level, null, args is { Length: > 0 } ? string.Format(format ?? "", args!) : format, eventCache);
    }

    public override void TraceTransfer(TraceEventCache? eventCache, string source, int id, string? message, Guid relatedActivityId)
    {
        LogWithStackTrace(GetLogLevel(TraceEventType.Transfer), null, $"{message} RelatedActivityId={relatedActivityId}", eventCache);
    }

    public override void Write(string? message)
        => Log(DetectLogLevel(message), null, $"{message}");

    public override void Write(object? o)
        => Log(LogLevel.Debug, null, $"{o}");

    public override void Write(string? message, string? category)
    {
        var level = DetectLogLevel(message);
        var formatted = !string.IsNullOrWhiteSpace(category) ? $"[{category}] {message}" : $"{message}";
        Log(level, null, formatted);
    }

    public override void Write(object? o, string? category)
    {
        var level = DetectLogLevel(o?.ToString());
        var formatted = !string.IsNullOrWhiteSpace(category) ? $"[{category}] {o}" : $"{o}";
        Log(level, null, formatted);
    }

    public override void WriteLine(string? message) => Write(message);
    public override void WriteLine(object? o) => Write(o);
    public override void WriteLine(string? message, string? category) => Write(message, category);
    public override void WriteLine(object? o, string? category) => Write(o, category);

    private void LogWithStackTrace(LogLevel level, Exception? exception, string? message, TraceEventCache? eventCache)
    {
        if (_options is { IncludeStackTrace: true, StackTraceDepth: > 0 } && eventCache != null)
        {
            var stackTrace = StackTraceBuilder.BuildStackTrace(eventCache, _options.StackTraceDepth);
            if (!string.IsNullOrWhiteSpace(stackTrace))
            {
                Log(level, exception, $"{message} | {stackTrace}");
                return;
            }
        }
        Log(level, exception, $"{message}");
    }

    private void Log(LogLevel level, Exception? exception, string? message)
    {
        _logger.ZLog(level, exception, $"{message}");
    }

    private static LogLevel GetLogLevel(TraceEventType eventType) => eventType switch
    {
        TraceEventType.Critical => LogLevel.Critical,
        TraceEventType.Error => LogLevel.Error,
        TraceEventType.Information => LogLevel.Information,
        TraceEventType.Warning => LogLevel.Warning,
        TraceEventType.Verbose => LogLevel.Trace,
        _ => LogLevel.Debug
    };

    private LogLevel DetectLogLevel(string? message)
    {
        return LogLevelDetector.Detect(message, _criticalKeywords, _errorKeywords, _warningKeywords, _informationKeywords);
    }

    private bool ShouldTrace(TraceEventCache? cache, string source, TraceEventType eventType, int id, string? formatOrMessage, object?[]? args, object? data1, object?[]? data)
    {
        var filter = Filter;
        return filter?.ShouldTrace(cache, source, eventType, id, formatOrMessage, args, data1, data) != false;
    }
}
