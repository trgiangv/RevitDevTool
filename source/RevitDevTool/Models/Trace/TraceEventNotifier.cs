using System.Diagnostics;

namespace RevitDevTool.Models.Trace;

/// <summary>
/// A TraceListener that raises an event whenever a trace message is received.
/// Used to trigger actions like showing the floating TraceLog window.
/// The event is raised only once per trace operation (not for each Write/WriteLine call).
/// </summary>
internal class TraceEventNotifier : TraceListener
{
    /// <summary>
    /// Event raised when any trace output is received.
    /// </summary>
    public static event Action? TraceReceived;

    public override bool IsThreadSafe => true;
    
    private static void RaiseTraceReceived()
    {
        TraceReceived?.Invoke();
    }

    public override void Write(string? message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            RaiseTraceReceived();
        }
    }

    public override void WriteLine(string? message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            RaiseTraceReceived();
        }
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id)
    {
        RaiseTraceReceived();
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
    {
        RaiseTraceReceived();
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args)
    {
        RaiseTraceReceived();
    }

    public override void TraceData(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, object? data)
    {
        RaiseTraceReceived();
    }

    public override void TraceData(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, params object?[]? data)
    {
        RaiseTraceReceived();
    }
}

