using DevTools.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DevTools.Telemetry;

/// <summary>
/// Host wiring for <see cref="ITelemetry"/>. Composition roots pass the enable switch;
/// <see cref="IHostAppInfo"/> is resolved from DI when Sentry is constructed.
/// </summary>
public static class TelemetryServiceExtensions
{
    public static IServiceCollection AddTelemetryService(
        this IServiceCollection services,
        Func<IServiceProvider, bool> isEnabled)
    {
        services.AddSingleton<ITelemetry>(sp =>
        {
            try
            {
                return isEnabled(sp)
                    ? new SentryTelemetry(sp.GetRequiredService<IHostAppInfo>())
                    : new NoOpTelemetry();
            }
            catch
            {
                return new NoOpTelemetry();
            }
        });
        return services;
    }
}
