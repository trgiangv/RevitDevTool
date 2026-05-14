using DevTools.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DevTools.Telemetry;

/// <summary>
/// Host wiring shared by Revit, AutoCAD, and any other host that registers <see cref="ISettingsService"/> and <see cref="Logging.IHostAppInfo"/>.
/// </summary>
public static class TelemetryExtensions
{
    public static HostApplicationBuilder AddDevToolsTelemetry(this HostApplicationBuilder builder)
    {
        // ReSharper disable once RedundantTypeArgumentsOfMethod
        builder.Services.AddSingleton<ITelemetry>(TelemetryServiceRegistration.Resolve);
        return builder;
    }
}
