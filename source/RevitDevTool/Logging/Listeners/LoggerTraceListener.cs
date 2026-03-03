using System.Diagnostics;
using RevitDevTool.Settings.Config;
using RevitDevTool.Utils;
using ILogger = Serilog.ILogger;
using Serilog.Events;
// ReSharper disable TemplateIsNotCompileTimeConstantProblem
#pragma warning disable CA2254
namespace RevitDevTool.Logging.Listeners;

/// <summary>
/// Host-agnostic trace listener writing to Serilog.
/// </summary>
public class LoggerTraceListener(ILogger logger, LogConfig options) : TraceListener
{
    private const string CategoryProperty = "Category";
    private const string StackTraceProperty = "StackTrace";
    private const string EventIdProperty = "TraceEventId";
    private const string FailDetailMessageProperty = "FailDetails";
    private const string RelatedActivityIdProperty = "RelatedActivityId";
    private const string SourceProperty = "TraceSource";
    private const string TraceEventTypeProperty = "TraceEventType";

    private readonly ILogger _logger = logger.ForContext<LoggerTraceListener>();
    private readonly LogConfig _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly bool _enableJsonSerialization = options.EnablePrettyJson;

    private readonly string[] _criticalKeywords = LogLevelDetector.ParseKeywords(options.FilterKeywords.Critical);
    private readonly string[] _errorKeywords = LogLevelDetector.ParseKeywords(options.FilterKeywords.Error);
    private readonly string[] _warningKeywords = LogLevelDetector.ParseKeywords(options.FilterKeywords.Warning);
    private readonly string[] _informationKeywords = LogLevelDetector.ParseKeywords(options.FilterKeywords.Information);

    public override bool IsThreadSafe => true;

    public override void Fail(string? message) => _logger.Fatal(message ?? string.Empty);

    public override void Fail(string? message, string? detailMessage)
        => _logger.ForContext(FailDetailMessageProperty, detailMessage).Fatal(message ?? string.Empty);

    public override void TraceData(TraceEventCache? eventCache,string source, TraceEventType eventType, int id, object? data)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, "", null, data, null)) return;
        var enriched = EnrichTraceContext(source, eventType, id, eventCache);
        WriteData(enriched, eventType, data);
    }

    public override void TraceData(TraceEventCache? eventCache,string source, TraceEventType eventType, int id, params object?[]? data)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, "", null, null, data)) return;
        var enriched = EnrichTraceContext(source, eventType, id, eventCache);
        WriteData(enriched, eventType, data);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, "", null, null, null)) return;
        var enriched = EnrichTraceContext(source, eventType, id, eventCache);
        enriched.Write(GetLogLevel(eventType), "{TraceSource:l} {TraceEventType}: {TraceEventId}", source, eventType, id);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, message, null, null, null)) return;
        var enriched = EnrichTraceContext(source, eventType, id, eventCache);
        enriched.Write(GetLogLevel(eventType), message ?? string.Empty);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, format, args, null, null)) return;
        var enriched = EnrichTraceContext(source, eventType, id, eventCache);
        var exception = ExtractFirstException(args);
        if (args is { Length: > 0 } && !string.IsNullOrEmpty(format))
        {
            // ReSharper disable once RedundantSuppressNullableWarningExpression
            var (template, convertedArgs) = ConvertToStructuredFormat(format!, args);
            enriched.Write(GetLogLevel(eventType), exception, template, convertedArgs);
        }
        else
        {
            enriched.Write(GetLogLevel(eventType), exception, format ?? string.Empty);
        }
    }

    public override void TraceTransfer(TraceEventCache? eventCache, string source, int id, string? message, Guid relatedActivityId)
    {
        var enriched = EnrichTraceContext(source, TraceEventType.Transfer, id, eventCache).ForContext(RelatedActivityIdProperty, relatedActivityId);
        enriched.Write(GetLogLevel(TraceEventType.Transfer), message ?? string.Empty);
    }

    public override void Write(object? data)
    {
        var level = DetectLogLevel(data?.ToString());
        if (IsElementIdType(data))
            _logger.Write(level, "{TraceData:l}", data?.ToString() ?? string.Empty);
        else
            _logger.Write(level, _enableJsonSerialization ? "{@TraceData:j}" : "{$TraceData}", data);
    }

    public override void Write(string? message)
    {
        var level = DetectLogLevel(message);
        _logger.Write(level, message ?? string.Empty);
    }

    public override void Write(object? data, string? category)
    {
        var level = DetectLogLevel(data?.ToString());
        if (IsElementIdType(data))
        {
            var value = data?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(category))
                _logger.ForContext(CategoryProperty, category).Write(level, "[{Category}] {TraceData:l}", category, value);
            else
                _logger.Write(level, "{TraceData:l}", value);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(category))
                _logger.ForContext(CategoryProperty, category).Write(level, _enableJsonSerialization ? "[{Category}] {@TraceData:j}" : "[{Category}] {$TraceData}", category, data);
            else
                _logger.Write(level, _enableJsonSerialization ? "{@TraceData:j}" : "{$TraceData}", data);
        }
    }

    public override void Write(string? message, string? category)
    {
        var level = DetectLogLevel(message);
        if (!string.IsNullOrWhiteSpace(category))
            _logger.ForContext(CategoryProperty, category).Write(level, message ?? string.Empty);
        else
            _logger.Write(level, message ?? string.Empty);
    }

    public override void WriteLine(string? message) => Write(message);
    public override void WriteLine(object? data) => Write(data);
    public override void WriteLine(string? message, string? category) => Write(message, category);
    public override void WriteLine(object? data, string? category) => Write(data, category);

    private ILogger EnrichTraceContext(string source, TraceEventType eventType, int id, TraceEventCache? eventCache)
    {
        var enriched = _logger.ForContext(SourceProperty, source).ForContext(TraceEventTypeProperty, eventType).ForContext(EventIdProperty, id);
        if (!_options.IncludeStackTrace || _options.StackTraceDepth <= 0 || eventCache == null) return enriched;
        var stackTrace = StackTraceUtils.BuildStackTrace(eventCache, _options.StackTraceDepth);
        if (!string.IsNullOrWhiteSpace(stackTrace))
            enriched = enriched.ForContext(StackTraceProperty, stackTrace);
        return enriched;
    }

    private void WriteData(ILogger logger, TraceEventType eventType, object? data)
    {
        if (IsElementIdType(data))
            logger.Write(GetLogLevel(eventType), "{TraceData:l}", data?.ToString() ?? string.Empty);
        else
            logger.Write(GetLogLevel(eventType), _enableJsonSerialization ? "{@TraceData:j}" : "{$TraceData}", data);
    }

    private (string template, object[] args) ConvertToStructuredFormat(string format, object?[] args)
    {
        var template = format;
        if (args.Length == 0)
        {
            return (template, []);
        }

        var convertedArgs = new object[args.Length];
        var convertedCount = 0;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is Exception)
            {
                continue;
            }

            if (TryConvertFormattedPlaceholder(template, i, out template))
            {
                convertedArgs[convertedCount++] = arg ?? "null";
                continue;
            }

            if (TryConvertSimplePlaceholder(template, i, IsComplexObject(arg), out template))
            {
                convertedArgs[convertedCount++] = arg ?? "null";
            }
        }

        return (template, SliceConvertedArgs(convertedArgs, convertedCount));
    }

    private static object[] SliceConvertedArgs(object[] convertedArgs, int convertedCount)
    {
        if (convertedCount == 0)
        {
            return Array.Empty<object>();
        }

        if (convertedCount == convertedArgs.Length)
        {
            return convertedArgs;
        }

        var finalArgs = new object[convertedCount];
        Array.Copy(convertedArgs, finalArgs, convertedCount);
        return finalArgs;
    }

    private static bool TryConvertFormattedPlaceholder(string template, int index, out string convertedTemplate)
    {
        var formatPlaceholderPrefix = $"{{{index}:";
        var formatStartIndex = template.IndexOf(formatPlaceholderPrefix, StringComparison.Ordinal);
        if (formatStartIndex < 0)
        {
            convertedTemplate = template;
            return false;
        }

        var formatEndIndex = template.IndexOf('}', formatStartIndex);
        if (formatEndIndex <= formatStartIndex)
        {
            convertedTemplate = template;
            return false;
        }

        var formatSpec = template.Substring(
            formatStartIndex + formatPlaceholderPrefix.Length,
            formatEndIndex - formatStartIndex - formatPlaceholderPrefix.Length);
        var originalPlaceholder = template.Substring(formatStartIndex, formatEndIndex - formatStartIndex + 1);
        convertedTemplate = template.Replace(originalPlaceholder, $"{{Arg{index}:{formatSpec}}}");
        return true;
    }

    private bool TryConvertSimplePlaceholder(string template, int index, bool isComplexObject, out string convertedTemplate)
    {
        var simplePlaceholder = $"{{{index}}}";
        if (!template.Contains(simplePlaceholder))
        {
            convertedTemplate = template;
            return false;
        }

        var structuredPlaceholder = isComplexObject
            ? _enableJsonSerialization ? $"{{@Arg{index}:j}}" : $"{{$Arg{index}}}"
            : $"{{Arg{index}}}";
        convertedTemplate = template.Replace(simplePlaceholder, structuredPlaceholder);
        return true;
    }

    private static Exception? ExtractFirstException(object?[]? args)
    {
        if (args == null) return null;

        foreach (var t in args)
        {
            if (t is Exception exception)
            {
                return exception;
            }
        }

        return null;
    }

    private static bool IsElementIdType(object? obj)
    {
        return obj != null && string.Equals(obj.GetType().Name, "ElementId", StringComparison.Ordinal);
    }

    private static bool IsComplexObject(object? obj)
    {
        if (obj == null) return false;
        var type = obj.GetType();
        if (type.IsPrimitive || type.IsEnum) return false;
        if (type == typeof(string) || type == typeof(decimal)) return false;
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return false;
        if (type == typeof(TimeSpan) || type == typeof(Guid) || type == typeof(Uri)) return false;
        return true;
    }

    private static LogEventLevel GetLogLevel(TraceEventType eventType) => eventType switch
    {
        TraceEventType.Critical => LogEventLevel.Fatal,
        TraceEventType.Error => LogEventLevel.Error,
        TraceEventType.Information => LogEventLevel.Information,
        TraceEventType.Warning => LogEventLevel.Warning,
        TraceEventType.Verbose => LogEventLevel.Verbose,
        _ => LogEventLevel.Debug
    };

    private LogEventLevel DetectLogLevel(string? message)
    {
        return LogLevelDetector.Detect(message, _criticalKeywords, _errorKeywords, _warningKeywords, _informationKeywords);
    }

    private bool ShouldTrace(TraceEventCache? cache, string source, TraceEventType eventType, int id, string? formatOrMessage, object?[]? args, object? data1, object?[]? data)
    {
        var filter = Filter;
        return filter?.ShouldTrace(cache, source, eventType, id, formatOrMessage, args, data1, data) != false;
    }
}
