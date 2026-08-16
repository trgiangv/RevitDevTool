using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DevTools.Telemetry;

/// <summary>
/// Host wiring for <see cref="ITelemetry"/>. Composition roots pass enable/DSN;
/// <see cref="DevTools.Hosting.IHostAppInfo"/> is resolved from DI when Sentry is constructed.
/// </summary>
public static class TelemetryExtensions
{
    public static HostApplicationBuilder AddDevToolsTelemetry(
        this HostApplicationBuilder builder,
        Func<IServiceProvider, bool> isEnabled,
        Func<IServiceProvider, string?> resolveDsn)
    {
        builder.Services.AddSingleton<ITelemetry>(sp =>
            TelemetryServiceRegistration.Resolve(sp, isEnabled, resolveDsn));
        return builder;
    }
}
