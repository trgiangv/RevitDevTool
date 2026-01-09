using System.Diagnostics;
using RevitDevTool.Logging;
using RevitDevTool.Services;

namespace RevitDevTool.Utils;

public static class TraceUtils
{
    public static void RegisterTraceListeners(params TraceListener?[] listeners)
    {
        foreach (var listener in listeners)
        {
            if (Trace.Listeners.Contains(listener) || listener == null) continue;
            Trace.Listeners.Add(listener);
            if (SettingsService.Instance.LogConfig.IncludeWpfTrace && listener is AdapterTraceListener)
                PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        }
    }
    
    public static void UnregisterTraceListeners(params TraceListener?[] listeners)
    {
        foreach (var listener in listeners)
        {
            if (!Trace.Listeners.Contains(listener) || listener == null) continue;
            Trace.Listeners.Remove(listener);
            if (SettingsService.Instance.LogConfig.IncludeWpfTrace && listener is AdapterTraceListener)
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
        }
    }
}
