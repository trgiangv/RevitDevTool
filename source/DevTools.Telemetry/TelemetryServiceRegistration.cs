using DevTools.Hosting;
using DevTools.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Telemetry;

/// <summary>
/// Registers <see cref="ITelemetry"/> for any host that exposes <see cref="ISettingsService"/> and <see cref="IHostAppInfo"/>.
/// </summary>
public static class TelemetryServiceRegistration
{
    public static ITelemetry Resolve(IServiceProvider services)
    {
        try
        {
            var settings = services.GetRequiredService<ISettingsService>();
            if (!settings.GeneralConfig.EnableTelemetry)
            {
                return new NoOpTelemetry();
            }

            var dsn = TelemetryDsnResolver.TryResolve(BuiltInSentryDsn.Value);
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
