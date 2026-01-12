using Autodesk.Windows;
using System.Diagnostics;

namespace RevitDevTool.Models.Trace;

/// <summary>
/// A TraceListener that raises an event whenever a trace message is received.
/// Used to trigger actions like showing the floating TraceLog window.
/// Only responds to trace events when Revit is fully initialized and ready.
/// </summary>
internal class NotifyListener : TraceListener
{
    /// <summary>
    /// Event raised when any trace output is received (only when Revit is active).
    /// </summary>
    public static event Action? TraceReceived;

    public override bool IsThreadSafe => true;

    /// <summary>
    /// Checks if we should respond to trace events.
    /// Only responds when Revit application frame is enabled.
    /// </summary>
    private static bool ShouldRespondToTraceEvent()
    {
        return ComponentManager.IsApplicationFrameEnabled;
    }

    private static void RaiseTraceReceived()
    {
        if (ShouldRespondToTraceEvent())
        {
            TraceReceived?.Invoke();
        }
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

