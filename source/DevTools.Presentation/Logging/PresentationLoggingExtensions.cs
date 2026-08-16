using DevTools.Presentation.Interfaces;
using DevTools.Presentation.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger.Scintilla.Public;

namespace DevTools.Presentation;

public static class PresentationLoggingExtensions
{
    /// <summary>
    /// Adds the monitor pane with historical Host.cs channel/display defaults.
    /// Optional <paramref name="configureMonitor"/> runs after those defaults (e.g. Revit linkification).
    /// </summary>
    public static HostApplicationBuilder AddMonitorLogging(
        this HostApplicationBuilder builder,
        Action<ScintillaOptions>? configureMonitor = null)
    {
        builder.Logging.AddMonitorLogging(v =>
        {
            v.Channel(capacity: 50_000, flushMs: 50, maxBatch: 800)
                .Display(maxLines: 50_000, fontSize: 9);
            configureMonitor?.Invoke(v);
        });
        return builder;
    }

    /// <summary>
    /// Adds monitor logging backed by <see cref="ScintillaLogViewerWpf"/>.
    /// ScintillaOptions are immutable after viewer creation.
    /// </summary>
    public static ILoggingBuilder AddMonitorLogging(
        this ILoggingBuilder builder,
        Action<ScintillaOptions>? configure = null)
    {
        builder.AddZLoggerScintillaWpf(configure);

        builder.Services.TryAddSingleton<IMonitorLogTarget>(sp =>
            new MonitorLogTarget(sp.GetRequiredService<ScintillaLogViewerWpf>()));

        return builder;
    }
}
