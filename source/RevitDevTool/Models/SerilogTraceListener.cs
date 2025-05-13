#nullable disable
using System.Diagnostics;
using System.Globalization;
using Serilog;
using Serilog.Events;
// ReSharper disable UnusedMember.Local

namespace RevitDevTool.Models;

/// <summary>
/// TraceListener implementation that directs all output to Serilog.
/// </summary>
internal class SerilogTraceListener : TraceListener
{
    private const string ActivityIdProperty = "ActivityId";
    private const string CategoryProperty = "Category";
    private const string EventIdProperty = "TraceEventId";
    private const string FailDetailMessageProperty = "FailDetails";
    private const string RelatedActivityIdProperty = "RelatedActivityId";
    private const string SourceProperty = "TraceSource";
    private const string TraceDataProperty = "TraceData";
    private const string TraceEventTypeProperty = "TraceEventType";
    private const LogEventLevel DefaultLogLevel = LogEventLevel.Debug;
    private const LogEventLevel FailLevel = LogEventLevel.Fatal;
    private const string NoMessageTraceEventMessageTemplate = "{TraceSource:l} {TraceEventType}: {TraceEventId}";
    private const string TraceDataMessageTemplate = "{TraceData}";
    private readonly ILogger _logger;

    /// <summary>
    ///     Creates a SerilogTraceListener that sets logger to null so we can still use Serilog's Logger.Log
    /// </summary>
    /// <remarks>
    ///     This is needed because TraceListeners are often configured through XML
    ///     where there would be no opportunity for constructor injection
    /// </remarks>
    public SerilogTraceListener() => _logger = null;

    /// <summary>
    ///     Creates a SerilogTraceListener that uses the specified logger
    /// </summary>
    public SerilogTraceListener(ILogger logger)
    {
        _logger = logger.ForContext<SerilogTraceListener>();
    }

    /// <summary>
    ///     Creates a SerilogTraceListener for the context specified.
    /// </summary>
    /// <example>
    ///     &lt;listeners&gt;
    ///         &lt;add name="Serilog" type="SerilogTraceListener.SerilogTraceListener, SerilogTraceListener" initializeData="MyContext" /&gt;
    ///     &lt;/listeners&gt;
    /// </example>
    public SerilogTraceListener(string context)
    {
        _logger = Log.Logger.ForContext("SourceContext", context);
    }

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
        SafeAddProperty(failProperties, "FailDetails", detailMessage);
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
        var traceProperties = CreateTraceProperties(source, eventType, id);
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
        var traceProperties = CreateTraceProperties(source, eventType, id);
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
        var traceProperties = CreateTraceProperties(source, eventType, id);
        Write(eventType, null, "{TraceSource:l} {TraceEventType}: {TraceEventId}", traceProperties);
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
        var traceProperties = CreateTraceProperties(source, eventType, id);
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
        var traceProperties = CreateTraceProperties(source, eventType, id);
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
        var eventType = TraceEventType.Transfer;
        var traceProperties = CreateTraceProperties(source, eventType, id);
        SafeAddProperty(traceProperties, "RelatedActivityId", relatedActivityId);
        Write(eventType, null, message, traceProperties);
    }

    /// <inheritdoc />
    public override void Write(object data)
    {
        var properties = CreateProperties();
        SafeAddProperty(properties, "TraceData", data);
        Write(LogEventLevel.Debug, null, "{TraceData}", properties);
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
        SafeAddProperty(properties, "TraceData", data);
        SafeAddProperty(properties, "Category", category);
        Write(LogEventLevel.Debug, null, "{TraceData}", properties);
    }

    /// <inheritdoc />
    public override void Write(string message, string category)
    {
        var properties = CreateProperties();
        SafeAddProperty(properties, "Category", category);
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
        SafeAddProperty(properties, "TraceEventType", "Fail");
        return properties;
    }

    private List<LogEventProperty> CreateProperties()
    {
        var properties = new List<LogEventProperty>();
        SafeAddProperty(properties, "ActivityId", Trace.CorrelationManager.ActivityId);
        return properties;
    }

    private List<LogEventProperty> CreateTraceProperties(
        string source,
        TraceEventType eventType,
        int id)
    {
        var properties = CreateProperties();
        SafeAddProperty(properties, "TraceSource", source);
        SafeAddProperty(properties, "TraceEventType", eventType);
        SafeAddProperty(properties, "TraceEventId", id);
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
        SafeAddProperty(properties, "TraceData", data);
        Write(logEventLevel, null, "{TraceData}", properties);
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