using DevTools.Logging.Abstractions;
using DevTools.Logging.Options;
using DevTools.Logging.Targets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ZLogger.Scintilla.Public;

namespace DevTools.Logging.Extensions;

public static class LoggingBuilderExtensions
{
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

    /// <summary>
    /// Adds file logging via <see cref="FileLogProcessor"/>.
    /// Optional <paramref name="configure"/> sets initial defaults.
    /// Runtime <c>Enable&lt;FileLoggingOptions&gt;(options)</c> overrides.
    /// </summary>
    public static ILoggingBuilder AddFileLogging(
        this ILoggingBuilder builder,
        Action<FileLoggingOptions>? configure = null)
    {
        if (configure != null)
        {
            var defaults = new FileLoggingOptions();
            configure(defaults);
            builder.Services.AddSingleton(new FileLoggingDefaults(defaults));
        }

        builder.Services.TryAddSingleton<IFileLogTarget, FileLogProcessor>();
        return builder;
    }

    /// <summary>
    /// Adds HTTP logging via <see cref="HttpLogProcessor"/>.
    /// Optional <paramref name="configure"/> sets initial defaults.
    /// Runtime <c>Enable&lt;HttpLoggingOptions&gt;(options)</c> overrides.
    /// </summary>
    public static ILoggingBuilder AddHttpLogging(
        this ILoggingBuilder builder,
        Action<HttpLoggingOptions>? configure = null)
    {
        if (configure != null)
        {
            var defaults = new HttpLoggingOptions();
            configure(defaults);
            builder.Services.AddSingleton(new HttpLoggingDefaults(defaults));
        }

        builder.Services.TryAddSingleton<IHttpLogTarget, HttpLogProcessor>();
        return builder;
    }
}

internal record FileLoggingDefaults(FileLoggingOptions Options);
internal record HttpLoggingDefaults(HttpLoggingOptions Options);
