using Microsoft.Extensions.Logging;
using RevitDevTool.Utils;
using System.Diagnostics;
using System.Globalization;

namespace RevitDevTool.Logging;

/// <summary>
/// TraceListener implementation that directs output to ILoggerAdapter.
/// Framework-agnostic trace listener that works with any logging backend.
/// </summary>
internal sealed class AdapterTraceListener(ILoggerAdapter logger, bool includeStackTrace, int stackTraceDepth) : TraceListener
{
    private const string RevitVersionProperty = "RevitVersion";
    private const string ActiveDocumentProperty = "ActiveDocument";
    private const string CategoryProperty = "Category";
    private const string StackTraceProperty = "StackTrace";
    private const string EventIdProperty = "TraceEventId";
    private const string FailDetailMessageProperty = "FailDetails";
    private const string RelatedActivityIdProperty = "RelatedActivityId";
    private const string SourceProperty = "TraceSource";
    private const string TraceDataProperty = "TraceData";
    private const string TraceEventTypeProperty = "TraceEventType";

    private readonly ILoggerAdapter _logger = logger.ForContext<AdapterTraceListener>();

    public override bool IsThreadSafe => true;

    public override void Fail(string? message)
    {
        var enrichedLogger = EnrichWithRevitContext(_logger);
        enrichedLogger.Fatal("{Message}", message ?? string.Empty);
    }

    public override void Fail(string? message, string? detailMessage)
    {
        var enrichedLogger = EnrichWithRevitContext(_logger)
            .ForContext(FailDetailMessageProperty, detailMessage);
        enrichedLogger.Fatal("{Message}", message ?? string.Empty);
    }

    public override void TraceData(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, object? data)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, "", null, data, null))
            return;

        var enrichedLogger = EnrichTraceContext(source, eventType, id, eventCache);
        WriteData(enrichedLogger, eventType, data);
    }

    public override void TraceData(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, params object?[]? data)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, "", null, null, data))
            return;

        var enrichedLogger = EnrichTraceContext(source, eventType, id, eventCache);
        WriteData(enrichedLogger, eventType, data);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, "", null, null, null))
            return;

        var enrichedLogger = EnrichTraceContext(source, eventType, id, eventCache);
        Write(enrichedLogger, eventType, "{TraceSource:l} {TraceEventType}: {TraceEventId}", source, eventType, id);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, message, null, null, null))
            return;

        var enrichedLogger = EnrichTraceContext(source, eventType, id, eventCache);
        Write(enrichedLogger, eventType, message ?? string.Empty);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, format, args, null, null))
            return;

        var enrichedLogger = EnrichTraceContext(source, eventType, id, eventCache);
        var message = args is { Length: > 0 }
            ? string.Format(CultureInfo.InvariantCulture, format ?? string.Empty, args)
            : format ?? string.Empty;

        var exception = args?.OfType<Exception>().FirstOrDefault();
        Write(enrichedLogger, eventType, exception, message);
    }

    public override void TraceTransfer(TraceEventCache? eventCache, string source, int id, string? message, Guid relatedActivityId)
    {
        var enrichedLogger = EnrichTraceContext(source, TraceEventType.Transfer, id, eventCache)
            .ForContext(RelatedActivityIdProperty, relatedActivityId);
        Write(enrichedLogger, TraceEventType.Transfer, message ?? string.Empty);
    }

    public override void Write(object? data)
    {
        var enrichedLogger = EnrichWithRevitContext(_logger)
            .ForContext(TraceDataProperty, data);
        enrichedLogger.Debug("{TraceData}", data);
    }

    public override void Write(string? message)
    {
        var enrichedLogger = EnrichWithRevitContext(_logger);
        enrichedLogger.Debug(message ?? string.Empty);
    }

    public override void Write(object? data, string? category)
    {
        var enrichedLogger = EnrichWithRevitContext(_logger)
            .ForContext(TraceDataProperty, data)
            .ForContext(CategoryProperty, category);

        if (!string.IsNullOrWhiteSpace(category))
            enrichedLogger.Debug("[{Category}] {TraceData}", category, data);
        else
            enrichedLogger.Debug("{TraceData}", data);
    }

    public override void Write(string? message, string? category)
    {
        var enrichedLogger = EnrichWithRevitContext(_logger)
            .ForContext(CategoryProperty, category);

        if (!string.IsNullOrWhiteSpace(category))
            enrichedLogger.Debug("[{Category}] {Message}", category, message);
        else
            enrichedLogger.Debug(message ?? string.Empty);
    }

    public override void WriteLine(string? message) => Write(message);
    public override void WriteLine(object? data) => Write(data);
    public override void WriteLine(string? message, string? category) => Write(message, category);
    public override void WriteLine(object? data, string? category) => Write(data, category);

    private static ILoggerAdapter EnrichWithRevitContext(ILoggerAdapter logger)
    {
        return logger
            .ForContext(RevitVersionProperty, $"Revit {Context.Application.VersionNumber}")
            .ForContext(ActiveDocumentProperty, Context.ActiveDocument?.Title ?? "No Active Document");
    }

    private ILoggerAdapter EnrichTraceContext(string source, TraceEventType eventType, int id, TraceEventCache? eventCache)
    {
        var enrichedLogger = EnrichWithRevitContext(_logger)
            .ForContext(SourceProperty, source)
            .ForContext(TraceEventTypeProperty, eventType)
            .ForContext(EventIdProperty, id);

        if (!includeStackTrace || stackTraceDepth <= 0 || eventCache == null) return enrichedLogger;

        var stackTrace = StackTraceUtils.BuildStackTrace(eventCache, stackTraceDepth);
        if (!string.IsNullOrWhiteSpace(stackTrace))
            enrichedLogger = enrichedLogger.ForContext(StackTraceProperty, stackTrace);

        return enrichedLogger;
    }

    private static void WriteData(ILoggerAdapter logger, TraceEventType eventType, object? data)
    {
        var level = GetLogLevel(eventType);
        logger.ForContext(TraceDataProperty, data).Write(level, "{TraceData}", data);
    }

    private static void Write(ILoggerAdapter logger, TraceEventType eventType, string messageTemplate, params object[] args)
    {
        logger.Write(GetLogLevel(eventType), messageTemplate, args);
    }

    private static void Write(ILoggerAdapter logger, TraceEventType eventType, Exception? exception, string message)
    {
        logger.Write(GetLogLevel(eventType), exception, message);
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

    private bool ShouldTrace(
        TraceEventCache? cache,
        string source,
        TraceEventType eventType,
        int id,
        string? formatOrMessage,
        object?[]? args,
        object? data1,
        object?[]? data)
    {
        var filter = Filter;
        return filter == null || filter.ShouldTrace(cache, source, eventType, id, formatOrMessage, args, data1, data);
    }
}

