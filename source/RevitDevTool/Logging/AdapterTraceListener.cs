using Microsoft.Extensions.Logging;
using RevitDevTool.Models.Config;
using RevitDevTool.Utils;
using System.Diagnostics;

namespace RevitDevTool.Logging;

/// <summary>
/// TraceListener implementation that directs output to ILoggerAdapter.
/// Framework-agnostic trace listener that works with any logging backend.
/// </summary>
internal sealed class AdapterTraceListener(
    ILoggerAdapter logger,
    bool includeStackTrace,
    int stackTraceDepth,
    LogFilterKeywords? filterKeywords = null) : TraceListener
{
    private const string CategoryProperty = "Category";
    private const string StackTraceProperty = "StackTrace";
    private const string EventIdProperty = "TraceEventId";
    private const string FailDetailMessageProperty = "FailDetails";
    private const string RelatedActivityIdProperty = "RelatedActivityId";
    private const string SourceProperty = "TraceSource";
    private const string TraceEventTypeProperty = "TraceEventType";

    private readonly ILoggerAdapter _logger = logger.ForContext<AdapterTraceListener>();
    private readonly LogFilterKeywords _filterKeywords = filterKeywords ?? new LogFilterKeywords();

    public override bool IsThreadSafe => true;

    public override void Fail(string? message)
    {
        _logger.Fatal(message ?? string.Empty);
    }

    public override void Fail(string? message, string? detailMessage)
    {
        _logger.ForContext(FailDetailMessageProperty, detailMessage)
            .Fatal(message ?? string.Empty);
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
        WriteMessage(enrichedLogger, eventType, message ?? string.Empty);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, format, args, null, null))
            return;

        var enrichedLogger = EnrichTraceContext(source, eventType, id, eventCache);
        var exception = args?.OfType<Exception>().FirstOrDefault();

        if (args is { Length: > 0 } && !string.IsNullOrEmpty(format))
        {
            // ReSharper disable once RedundantSuppressNullableWarningExpression
            var (template, convertedArgs) = ConvertToStructuredFormat(format!, args);
            WriteStructured(enrichedLogger, eventType, exception, template, convertedArgs);
        }
        else
        {
            Write(enrichedLogger, eventType, exception, format ?? string.Empty);
        }
    }

    public override void TraceTransfer(TraceEventCache? eventCache, string source, int id, string? message, Guid relatedActivityId)
    {
        var enrichedLogger = EnrichTraceContext(source, TraceEventType.Transfer, id, eventCache)
            .ForContext(RelatedActivityIdProperty, relatedActivityId);
        Write(enrichedLogger, TraceEventType.Transfer, message ?? string.Empty);
    }

    public override void Write(object? data)
    {
        var level = DetectLogLevel(data?.ToString());
        _logger.Write(level, "{@TraceData:j}", data);
    }

    public override void Write(string? message)
    {
        var level = DetectLogLevel(message);
        _logger.Write(level, message ?? string.Empty);
    }

    public override void Write(object? data, string? category)
    {
        var level = DetectLogLevel(data?.ToString());

        if (!string.IsNullOrWhiteSpace(category))
        {
            _logger.ForContext(CategoryProperty, category)
                .Write(level, "[{Category}] {@TraceData:j}", category, data);
        }
        else
        {
            _logger.Write(level, "{@TraceData:j}", data);
        }
    }

    public override void Write(string? message, string? category)
    {
        var level = DetectLogLevel(message);

        if (!string.IsNullOrWhiteSpace(category))
        {
            _logger.ForContext(CategoryProperty, category)
                        .Write(level, "[{Category}] {Message}", category, message);
        }
        else
        {
            _logger.Write(level, message ?? string.Empty);
        }
    }

    public override void WriteLine(string? message) => Write(message);
    public override void WriteLine(object? data) => Write(data);
    public override void WriteLine(string? message, string? category) => Write(message, category);
    public override void WriteLine(object? data, string? category) => Write(data, category);


    private ILoggerAdapter EnrichTraceContext(string source, TraceEventType eventType, int id, TraceEventCache? eventCache)
    {
        var enrichedLogger = _logger
            .ForContext(SourceProperty, source)
            .ForContext(TraceEventTypeProperty, eventType)
            .ForContext(EventIdProperty, id);

        if (!includeStackTrace || stackTraceDepth <= 0 || eventCache == null)
            return enrichedLogger;

        var stackTrace = StackTraceUtils.BuildStackTrace(eventCache, stackTraceDepth);
        if (!string.IsNullOrWhiteSpace(stackTrace))
            enrichedLogger = enrichedLogger.ForContext(StackTraceProperty, stackTrace);

        return enrichedLogger;
    }

    private static void WriteData(ILoggerAdapter logger, TraceEventType eventType, object? data)
    {
        logger.Write(GetLogLevel(eventType), "{@TraceData:j}", data);
    }

    private static void Write(ILoggerAdapter logger, TraceEventType eventType, string messageTemplate, params object[] args)
    {
        logger.Write(GetLogLevel(eventType), messageTemplate, args);
    }

    private static void Write(ILoggerAdapter logger, TraceEventType eventType, Exception? exception, string message)
    {
        logger.Write(GetLogLevel(eventType), exception, message);
    }

    private static void WriteMessage(ILoggerAdapter logger, TraceEventType eventType, string message)
    {
        logger.Write(GetLogLevel(eventType), message);
    }

    private static void WriteStructured(ILoggerAdapter logger, TraceEventType eventType, Exception? exception, string messageTemplate, object[] args)
    {
        logger.Write(GetLogLevel(eventType), exception, messageTemplate, args);
    }


    /// <summary>
    /// Converts .NET string.Format style placeholders to structured logging placeholders.
    /// Preserves format specifiers: "{0:P2}" → "{Arg0:P2}", "{0}" → "{Arg0}"
    /// Complex objects use destructuring: "{@Arg0:j}"
    /// </summary>
    private static (string template, object[] args) ConvertToStructuredFormat(string format, object?[] args)
    {
        var template = format;
        var convertedArgs = new List<object>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is Exception) continue;

            // Match both {0} and {0:format} patterns
            var simplePlaceholder = $"{{{i}}}";
            var formatPlaceholderPrefix = $"{{{i}:";

            // Check for format specifier first: {0:P2}, {0:N2}, etc.
            var formatStartIndex = template.IndexOf(formatPlaceholderPrefix, StringComparison.Ordinal);
            if (formatStartIndex >= 0)
            {
                var formatEndIndex = template.IndexOf('}', formatStartIndex);
                if (formatEndIndex <= formatStartIndex) continue;
                var formatSpec = template.Substring(formatStartIndex + formatPlaceholderPrefix.Length,
                    formatEndIndex - formatStartIndex - formatPlaceholderPrefix.Length);
                var originalPlaceholder = template.Substring(formatStartIndex, formatEndIndex - formatStartIndex + 1);
                var structuredPlaceholder = $"{{Arg{i}:{formatSpec}}}";
                template = template.Replace(originalPlaceholder, structuredPlaceholder);
                convertedArgs.Add(arg ?? "null");
            }
            else if (template.Contains(simplePlaceholder))
            {
                // Simple placeholder without format specifier
                var structuredPlaceholder = IsComplexObject(arg)
                    ? $"{{@Arg{i}:j}}"
                    : $"{{Arg{i}}}";
                template = template.Replace(simplePlaceholder, structuredPlaceholder);
                convertedArgs.Add(arg ?? "null");
            }
        }

        return (template, convertedArgs.ToArray());
    }

    /// <summary>
    /// Determines if an object is complex (should be destructured) vs primitive.
    /// </summary>
    private static bool IsComplexObject(object? obj)
    {
        if (obj == null) return false;

        var type = obj.GetType();

        if (type.IsPrimitive || type.IsEnum) return false;
        if (type == typeof(string) || type == typeof(decimal)) return false;
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return false;
        if (type == typeof(TimeSpan) || type == typeof(Guid)) return false;
        if (type == typeof(Uri)) return false;

        return true;
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
        return TraceUtils.DetectLogLevel(message, _filterKeywords);
    }

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
        return filter?.ShouldTrace(cache, source, eventType, id, formatOrMessage, args, data1, data) != false;
    }
}
