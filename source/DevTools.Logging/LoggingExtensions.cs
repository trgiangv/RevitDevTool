using DevTools.Logging.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger.Scintilla.Public;
namespace DevTools.Logging;

/// <summary>
/// Default logging pipeline for DevTools add-in hosts (monitor + file + HTTP).
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Registers <see cref="LoggingConfiguration"/> and wires logging providers.
    /// Monitor defaults match historical Host.cs (channel/display); optional <paramref name="configureMonitor"/>
    /// runs after those defaults (e.g. Revit linkification).
    /// </summary>
    public static HostApplicationBuilder AddLoggingProvider(
        this HostApplicationBuilder builder,
        Action<ScintillaOptions>? configureMonitor = null)
    {
        var loggingConfig = new LoggingConfiguration();
        builder.Services.AddSingleton(loggingConfig);

        builder.Logging
            .AddConfiguration(loggingConfig.Configuration.GetSection("Logging"))
            .ClearProviders()
            .AddMonitorLogging(v =>
            {
                v.Channel(capacity: 50_000, flushMs: 50, maxBatch: 800)
                    .Display(maxLines: 50_000, fontSize: 9);
                configureMonitor?.Invoke(v);
            })
            .AddFileLogging()
            .AddHttpLogging();

        return builder;
    }
}
