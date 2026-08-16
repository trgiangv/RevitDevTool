using System.Diagnostics;
using DevTools.Logging.Listeners;

namespace DevTools.Presentation;

public static class PresentationTraceListenerHelper
{
    /// <summary>
    /// Aligns WPF <see cref="PresentationTraceSources"/> with log settings.
    /// </summary>
    public static void ApplyPresentationTraceSwitches(SourceLevels level)
    {
        PresentationTraceSources.AnimationSource.Switch.Level = level;
        PresentationTraceSources.DataBindingSource.Switch.Level = level;
        PresentationTraceSources.DependencyPropertySource.Switch.Level = level;
        PresentationTraceSources.FreezableSource.Switch.Level = level;
        PresentationTraceSources.HwndHostSource.Switch.Level = level;
        PresentationTraceSources.DocumentsSource.Switch.Level = level;
        PresentationTraceSources.MarkupSource.Switch.Level = level;
        PresentationTraceSources.NameScopeSource.Switch.Level = level;
        PresentationTraceSources.ResourceDictionarySource.Switch.Level = level;
        PresentationTraceSources.RoutedEventSource.Switch.Level = level;
    }

    private static TraceListenerCollection[] GetWpfTraceSources(bool includeWpfTrace)
    {
        if (!includeWpfTrace)
            return [];

        return
        [
            PresentationTraceSources.AnimationSource.Listeners,
            PresentationTraceSources.DataBindingSource.Listeners,
            PresentationTraceSources.DependencyPropertySource.Listeners,
            PresentationTraceSources.FreezableSource.Listeners,
            PresentationTraceSources.HwndHostSource.Listeners,
            PresentationTraceSources.DocumentsSource.Listeners,
            PresentationTraceSources.MarkupSource.Listeners,
            PresentationTraceSources.NameScopeSource.Listeners,
            PresentationTraceSources.ResourceDictionarySource.Listeners,
            PresentationTraceSources.RoutedEventSource.Listeners,
        ];
    }

    public static void RegisterWpfTraceListeners(bool includeWpfTrace, params TraceListener?[] listeners)
    {
        var wpfTraceSources = GetWpfTraceSources(includeWpfTrace);
        foreach (var listener in listeners)
        {
            if (listener == null) continue;

            foreach (var source in wpfTraceSources)
            {
                if (listener is LoggerTraceListener && !source.Contains(listener))
                    source.Add(listener);
            }
        }
    }

    /// <summary>
    /// Removes listeners from all WPF presentation trace sources.
    /// Unregister must not depend on <c>IncludeWpfTrace</c> or listeners can remain on WPF sources.
    /// </summary>
    public static void UnregisterWpfTraceListeners(params TraceListener?[] listeners)
    {
        var wpfTraceSources = GetWpfTraceSources(includeWpfTrace: true);
        foreach (var listener in listeners)
        {
            if (listener == null) continue;
            foreach (var source in wpfTraceSources)
                source.Remove(listener);
        }
    }
}
