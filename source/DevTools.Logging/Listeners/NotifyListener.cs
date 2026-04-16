using System.Diagnostics;

namespace DevTools.Logging.Listeners;

public sealed class NotifyListener : TraceListener
{
    public static event Action? TraceReceived;

    public override bool IsThreadSafe => true;

    public override void Write(string? message)
    {
        if (!string.IsNullOrEmpty(message))
            TraceReceived?.Invoke();
    }

    public override void WriteLine(string? message)
    {
        if (!string.IsNullOrEmpty(message))
            TraceReceived?.Invoke();
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id)
    {
        TraceReceived?.Invoke();
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
    {
        TraceReceived?.Invoke();
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args)
    {
        TraceReceived?.Invoke();
    }

    public override void TraceData(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, object? data)
    {
        TraceReceived?.Invoke();
    }

    public override void TraceData(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, params object?[]? data)
    {
        TraceReceived?.Invoke();
    }
}
