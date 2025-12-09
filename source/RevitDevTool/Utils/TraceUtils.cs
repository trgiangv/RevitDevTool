using System.Diagnostics;
using RevitDevTool.Models.Trace;
using RevitDevTool.Services;
namespace RevitDevTool.Utils;

public static class TraceUtils
{
    /// <summary>
    /// Register trace listeners to the Trace.Listeners collection.
    /// </summary>
    /// <param name="listeners"></param>
    public static void RegisterTraceListeners(params TraceListener?[] listeners)
    {
        foreach (var listener in listeners)
        {
            if (Trace.Listeners.Contains(listener) || listener == null) continue;
            Trace.Listeners.Add(listener);
            if (SettingsService.Instance.LogConfig.IncludeWpfTrace && listener is SerilogTraceListener)
                PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        }
    }
    
    /// <summary>
    /// Unregister trace listeners from the Trace.Listeners collection.
    /// </summary>
    /// <param name="listeners"></param>
    public static void UnregisterTraceListeners(params TraceListener?[] listeners)
    {
        foreach (var listener in listeners)
        {
            if (!Trace.Listeners.Contains(listener) || listener == null) continue;
            Trace.Listeners.Remove(listener);
            if (SettingsService.Instance.LogConfig.IncludeWpfTrace && listener is SerilogTraceListener)
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
        }
    }
}