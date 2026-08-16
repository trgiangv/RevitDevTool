using System.Diagnostics;

namespace DevTools.Logging;

public static class TraceListenerHelper
{
    public static void RegisterTraceListeners(params TraceListener?[] listeners)
    {
        foreach (var listener in listeners)
        {
            if (listener == null) continue;

            if (!Trace.Listeners.Contains(listener))
            {
                Trace.Listeners.Add(listener);
            }
        }
    }

    public static void UnregisterTraceListeners(params TraceListener?[] listeners)
    {
        foreach (var listener in listeners)
        {
            if (listener == null) continue;
            Trace.Listeners.Remove(listener);
        }
    }
}
