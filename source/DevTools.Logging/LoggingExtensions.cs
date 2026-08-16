using DevTools.Logging.Extensions;
using DevTools.Logging.Listeners;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging;

/// <summary>
/// Headless logging pipeline for DevTools (notify + file + HTTP). Monitor pane is Presentation opt-in.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Registers <see cref="LoggingConfiguration"/> and wires headless logging providers.
    /// </summary>
    public static HostApplicationBuilder AddLoggingProvider(this HostApplicationBuilder builder)
    {
        var loggingConfig = new LoggingConfiguration();

        builder.Services
            .AddSingleton(loggingConfig);

        builder.Logging
            .AddConfiguration(loggingConfig.LoggingSection)
            .ClearProviders()
            .AddProvider(new NotifyLoggerProvider())
            .AddFileLogging()
            .AddHttpLogging();

        return builder;
    }
}
