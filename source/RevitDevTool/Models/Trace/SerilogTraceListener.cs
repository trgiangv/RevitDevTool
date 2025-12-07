#nullable disable
using System.Diagnostics;
using System.Globalization;
using RevitDevTool.Utils;
using Serilog;
using Serilog.Events;

namespace RevitDevTool.Models.Trace;

/// <summary>
/// TraceListener implementation that directs all output to Serilog.
/// </summary>
internal sealed class SerilogTraceListener(ILogger logger, bool includeStackTrace, int stackTraceDepth) : TraceListener
{
    private const string RevitVersionProperty = "RevitVersion";
    private const string ActiveDocumentProperty = "ActiveDocument";
    private const string CategoryProperty = "Category";
    private const string StackTraceProperty = "StackTrace";
    private const string StackTraceDepthProperty = "StackTraceDepth";
    private const string EventIdProperty = "TraceEventId";
    private const string FailDetailMessageProperty = "FailDetails";
    private const string RelatedActivityIdProperty = "RelatedActivityId";
    private const string SourceProperty = "TraceSource";
    private const string TraceDataProperty = "TraceData";
    private const string TraceEventTypeProperty = "TraceEventType";
    private const string NoMessageTraceEventMessageTemplate = "{TraceSource:l} {TraceEventType}: {TraceEventId}";
    private const string TraceDataMessageTemplate = "{TraceData}";
    private readonly ILogger _logger = logger?.ForContext<SerilogTraceListener>();

    /// <inheritdoc />
    public override bool IsThreadSafe => true;

    /// <inheritdoc />
    public override void Fail(string message)
    {
        var failProperties = CreateFailProperties();
        Write(LogEventLevel.Fatal, null, message, failProperties);
    }

    /// <inheritdoc />
    public override void Fail(string message, string detailMessage)
    {
        var failProperties = CreateFailProperties();
        SafeAddProperty(failProperties, FailDetailMessageProperty, detailMessage);
        Write(LogEventLevel.Fatal, null, message, failProperties);
    }

    /// <inheritdoc />
    public override void TraceData(
        TraceEventCache eventCache,
        string source,
        TraceEventType eventType,
        int id,
        object data)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, "", null, data, null))
            return;
        var traceProperties = CreateTraceProperties(source, eventType, id, eventCache);
        WriteData(eventType, traceProperties, data);
    }

    /// <inheritdoc />
    public override void TraceData(
        TraceEventCache eventCache,
        string source,
        TraceEventType eventType,
        int id,
        params object[] data)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, "", null, null, data))
            return;
        var traceProperties = CreateTraceProperties(source, eventType, id, eventCache);
        WriteData(eventType, traceProperties, data);
    }

    /// <inheritdoc />
    public override void TraceEvent(
        TraceEventCache eventCache,
        string source,
        TraceEventType eventType,
        int id)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, "", null, null, null))
            return;
        var traceProperties = CreateTraceProperties(source, eventType, id, eventCache);
        Write(eventType, null, NoMessageTraceEventMessageTemplate, traceProperties);
    }

    /// <inheritdoc />
    public override void TraceEvent(
        TraceEventCache eventCache,
        string source,
        TraceEventType eventType,
        int id,
        string message)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, message, null, null, null))
            return;
        var traceProperties = CreateTraceProperties(source, eventType, id, eventCache);
        Write(eventType, null, message, traceProperties);
    }

    /// <inheritdoc />
    public override void TraceEvent(
        TraceEventCache eventCache,
        string source,
        TraceEventType eventType,
        int id,
        string format,
        params object[] args)
    {
        if (!ShouldTrace(eventCache, source, eventType, id, format, args, null, null))
            return;
        var traceProperties = CreateTraceProperties(source, eventType, id, eventCache);
        AddFormatArgs(traceProperties, args, out var exception);
        Write(eventType, exception, format, traceProperties);
    }

    /// <inheritdoc />
    public override void TraceTransfer(
        TraceEventCache eventCache,
        string source,
        int id,
        string message,
        Guid relatedActivityId)
    {
        const TraceEventType eventType = TraceEventType.Transfer;
        var traceProperties = CreateTraceProperties(source, eventType, id, eventCache);
        SafeAddProperty(traceProperties, RelatedActivityIdProperty, relatedActivityId);
        Write(eventType, null, message, traceProperties);
    }

    /// <inheritdoc />
    public override void Write(object data)
    {
        var properties = CreateProperties();
        SafeAddProperty(properties, TraceDataProperty, data);
        Write(LogEventLevel.Debug, null, TraceDataMessageTemplate, properties);
    }

    /// <inheritdoc />
    public override void Write(string message)
    {
        var properties = CreateProperties();
        Write(LogEventLevel.Debug, null, message, properties);
    }

    /// <inheritdoc />
    public override void Write(object data, string category)
    {
        var properties = CreateProperties();
        SafeAddProperty(properties, TraceDataProperty, data);
        SafeAddProperty(properties, CategoryProperty, category);
        Write(LogEventLevel.Debug, null, TraceDataMessageTemplate, properties);
    }

    /// <inheritdoc />
    public override void Write(string message, string category)
    {
        var properties = CreateProperties();
        SafeAddProperty(properties, CategoryProperty, category);
        Write(LogEventLevel.Debug, null, message, properties);
    }

    /// <inheritdoc />
    public override void WriteLine(string message) => Write(message);

    /// <inheritdoc />
    public override void WriteLine(object data) => Write(data);

    /// <inheritdoc />
    public override void WriteLine(string message, string category) => Write(message, category);

    /// <inheritdoc />
    public override void WriteLine(object data, string category) => Write(data, category);

    private void AddFormatArgs(
        IList<LogEventProperty> properties,
        object[] args,
        out Exception exception)
    {
        exception = null;
        if (args == null)
            return;
        for (var index = 0; index < args.Length; ++index)
        {
            SafeAddProperty(properties, index.ToString(CultureInfo.InvariantCulture), args[index]);
            if (args[index] is Exception)
                exception = (Exception)args[index];
        }
    }

    private List<LogEventProperty> CreateFailProperties()
    {
        var properties = CreateProperties();
        SafeAddProperty(properties, TraceEventTypeProperty, "Fail");
        return properties;
    }

    private List<LogEventProperty> CreateProperties([CanBeNull] TraceEventCache eventCache = null)
    {
        var properties = new List<LogEventProperty>();
        AddRevitContextProperties(properties);
        AddCallStackProperty(properties, eventCache);
        return properties;
    }

    private void AddRevitContextProperties(IList<LogEventProperty> properties)
    {
        SafeAddProperty(properties, RevitVersionProperty, $"Revit {Context.Application.VersionNumber}");
        SafeAddProperty(properties, ActiveDocumentProperty,  Context.ActiveDocument?.Title ?? "No Active Document");
    }
    
    private void AddCallStackProperty(IList<LogEventProperty> properties, [CanBeNull] TraceEventCache eventCache)
    {
        if (!includeStackTrace || stackTraceDepth <= 0 || eventCache is null)
            return;

        SafeAddProperty(properties, StackTraceDepthProperty, stackTraceDepth);
        
        var formattedStack = StackTraceUtils.BuildStackTrace(
            eventCache, 
            stackTraceDepth);
            
        if (!string.IsNullOrWhiteSpace(formattedStack))
            SafeAddProperty(properties, StackTraceProperty, formattedStack);
    }

    private List<LogEventProperty> CreateTraceProperties(
        string source,
        TraceEventType eventType,
        int id,
        TraceEventCache eventCache)
    {
        var properties = CreateProperties(eventCache);
        SafeAddProperty(properties, SourceProperty, source);
        SafeAddProperty(properties, TraceEventTypeProperty, eventType);
        SafeAddProperty(properties, EventIdProperty, id);
        return properties;
    }

    private void SafeAddProperty(IList<LogEventProperty> properties, string name, object value)
    {
        if (!(_logger ?? Log.Logger).BindProperty(name, value, false, out var property))
            return;
        properties.Add(property);
    }

    private void Write(
        TraceEventType eventType,
        Exception exception,
        string messageTemplate,
        IList<LogEventProperty> properties)
    {
        Write(LevelMapping.ToLogEventLevel(eventType), exception, messageTemplate, properties);
    }

    private void Write(
        LogEventLevel level,
        Exception exception,
        string messageTemplate,
        IList<LogEventProperty> properties)
    {
        messageTemplate ??= string.Empty;
        var logger = _logger ?? Log.Logger;
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        if (!logger.BindMessageTemplate(messageTemplate, null, out var parsedTemplate, out _))
            return;
        var logEvent = new LogEvent(DateTimeOffset.Now, level, exception, parsedTemplate, properties);
        logger.Write(logEvent);
    }

    private bool ShouldTrace(
        TraceEventCache cache,
        string source,
        TraceEventType eventType,
        int id,
        string formatOrMessage,
        object[] args,
        object data1,
        object[] data)
    {
        var filter = Filter;
        return filter == null || filter.ShouldTrace(cache, source, eventType, id, formatOrMessage, args, data1, data);
    }

    private void WriteData(TraceEventType eventType, IList<LogEventProperty> properties, object data)
    {
        var logEventLevel = LevelMapping.ToLogEventLevel(eventType);
        SafeAddProperty(properties, TraceDataProperty, data);
        Write(logEventLevel, null, TraceDataMessageTemplate, properties);
    }
}

internal static class LevelMapping
{
    public static LogEventLevel ToLogEventLevel(TraceEventType eventType)
    {
        return eventType switch
        {
            TraceEventType.Critical => LogEventLevel.Fatal,
            TraceEventType.Error => LogEventLevel.Error,
            TraceEventType.Information => LogEventLevel.Information,
            TraceEventType.Warning => LogEventLevel.Warning,
            TraceEventType.Verbose => LogEventLevel.Verbose,
            _ => LogEventLevel.Debug
        };
    }
}