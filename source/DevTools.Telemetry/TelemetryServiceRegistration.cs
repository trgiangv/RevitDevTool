using DevTools.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Telemetry;

/// <summary>
/// Resolves <see cref="ITelemetry"/> from composition-root enable/DSN callbacks and <see cref="IHostAppInfo"/>.
/// </summary>
public static class TelemetryServiceRegistration
{
    public static ITelemetry Resolve(
        IServiceProvider services,
        Func<IServiceProvider, bool> isEnabled,
        Func<IServiceProvider, string?> resolveDsn)
    {
        try
        {
            if (!isEnabled(services))
            {
                return new NoOpTelemetry();
            }

            var dsn = TelemetryDsnResolver.TryResolve(resolveDsn(services));
            if (string.IsNullOrWhiteSpace(dsn))
            {
                return new NoOpTelemetry();
            }

            var host = services.GetRequiredService<IHostAppInfo>();
            return new SentryTelemetryService(dsn!, host);
        }
        catch
        {
            return new NoOpTelemetry();
        }
    }
}
