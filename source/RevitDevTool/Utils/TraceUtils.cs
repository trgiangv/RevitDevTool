using RevitDevTool.Logging;
using System.Diagnostics;

namespace RevitDevTool.Utils;

public static class TraceUtils
{
    public static void RegisterTraceListeners(bool includeWpfTrace, params TraceListener?[] listeners)
    {
        foreach (var listener in listeners)
        {
            if (Trace.Listeners.Contains(listener) || listener == null) continue;
            Trace.Listeners.Add(listener);
            if (includeWpfTrace && listener is AdapterTraceListener)
                PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        }
    }

    public static void UnregisterTraceListeners(bool includeWpfTrace, params TraceListener?[] listeners)
    {
        foreach (var listener in listeners)
        {
            if (!Trace.Listeners.Contains(listener) || listener == null) continue;
            Trace.Listeners.Remove(listener);
            if (includeWpfTrace && listener is AdapterTraceListener)
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
        }
    }
}
