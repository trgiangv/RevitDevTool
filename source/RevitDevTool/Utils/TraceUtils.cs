using System.Diagnostics;
using RevitDevTool.Logger.Listeners;

namespace RevitDevTool.Utils;

public static class TraceUtils
{
    public static void RegisterTraceListeners(bool includeWpfTrace, params TraceListener?[] listeners)
    {
        foreach (var listener in listeners)
        {
            if (listener == null || Trace.Listeners.Contains(listener)) continue;
            Trace.Listeners.Add(listener);
            if (includeWpfTrace && listener is LoggerTraceListener)
                PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        }
    }

    public static void UnregisterTraceListeners(bool includeWpfTrace, params TraceListener?[] listeners)
    {
        foreach (var listener in listeners)
        {
            if (listener == null || !Trace.Listeners.Contains(listener)) continue;
            Trace.Listeners.Remove(listener);
            if (includeWpfTrace && listener is LoggerTraceListener)
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
        }
    }
}
