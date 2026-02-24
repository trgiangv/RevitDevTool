using System.Diagnostics;
using RevitDevTool.Logger.Transport;

namespace RevitDevTool.Logger.Listeners;

/// <summary>
/// Forwards trace events to an <see cref="ILogEventSink"/> without blocking caller threads.
/// </summary>
public sealed class PipeLogTraceListener : TraceListener
{
    private readonly ILogEventSink _sink;
    private readonly Func<string, string> _levelResolver;

    public PipeLogTraceListener(ILogEventSink sink, Func<string, string>? levelResolver = null)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _levelResolver = levelResolver ?? (_ => "Information");
    }

    public override bool IsThreadSafe => true;

    public override void Write(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        Publish("Trace", message!, string.Empty);
    }

    public override void WriteLine(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        Publish("Trace", message!, string.Empty);
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        Publish(eventType.ToString(), message!, source);
    }

    public override void TraceData(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, object? data)
    {
        if (data == null) return;
        Publish(eventType.ToString(), data.ToString() ?? string.Empty, source);
    }

    private void Publish(string traceLevel, string message, string source)
    {
        var level = _levelResolver(message);
        var evt = new LogEventData
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Level = string.IsNullOrWhiteSpace(level) ? traceLevel : level,
            Message = message,
            Source = source
        };

        _ = _sink.PublishAsync(evt, CancellationToken.None);
    }
}
