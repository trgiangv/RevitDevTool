using System.Diagnostics;

namespace DevTools.Logging;

public static class TraceListenerHelper
{
    public static void RegisterTraceListeners(bool includeWpfTrace, params TraceListener?[] listeners)
    {
        foreach (var listener in listeners)
        {
            if (listener == null || Trace.Listeners.Contains(listener)) continue;
            Trace.Listeners.Add(listener);
            if (includeWpfTrace)
                PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        }
    }

    public static void UnregisterTraceListeners(bool includeWpfTrace, params TraceListener?[] listeners)
    {
        foreach (var listener in listeners)
        {
            if (listener == null || !Trace.Listeners.Contains(listener)) continue;
            Trace.Listeners.Remove(listener);
            if (includeWpfTrace)
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
        }
    }
}
