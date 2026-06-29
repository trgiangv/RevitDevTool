using System.Windows;
using DevTools.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using ZLogger.Scintilla.Public;
namespace DevTools.Logging.Targets;

/// <summary>
/// Thin wrapper around <see cref="ScintillaLogViewerWpf"/> implementing <see cref="IMonitorLogTarget"/>.
/// Moves Scintilla-specific knowledge into DevTools.Logging so consumers only see the interface.
/// </summary>
public sealed class MonitorLogTarget(ScintillaLogViewerWpf viewer) : IMonitorLogTarget
{
    public FrameworkElement HostElement =>
        viewer.HostElement as FrameworkElement
        ?? throw new InvalidOperationException("Viewer host element is not a FrameworkElement.");

    public void Enable<T>(T options)
    {
        viewer.Start();
    }

    public void Disable()
    {
        viewer.Stop();
    }

    public void SetFilter(LogLevel level) => viewer.SetFilter(level);

    public void SetTheme(bool isDark) =>
        viewer.SetTheme(isDark ? ScintillaThemes.Dark : ScintillaThemes.Light);

    public void SetPrettyJson(bool enabled) => viewer.SetPrettyJson(enabled);

    public void Clear() => viewer.Clear();

    public void Dispose()
    {
        Disable();
        viewer.Dispose();
    }
}
