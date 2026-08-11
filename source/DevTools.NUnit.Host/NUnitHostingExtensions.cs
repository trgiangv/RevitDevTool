using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DevTools.NUnit.Host;

public static class NUnitHostingExtensions
{
    /// <summary>
    /// Registers the in-host reflective NUnit runner and bridge handler.
    /// Call from Revit/AutoCAD hosting after <c>AddExecutionServices()</c>.
    /// </summary>
    public static IServiceCollection AddNUnitHostServices(this IServiceCollection services)
    {
        services.TryAddSingleton<NUnitAssemblyLoader>();
        services.TryAddSingleton<NUnitReflectionRunner>(sp =>
            new NUnitReflectionRunner(
                sp.GetRequiredService<NUnitAssemblyLoader>(),
                sp.GetService<ILogger<NUnitReflectionRunner>>()));
        services.TryAddSingleton<INUnitHost, NUnitHost>();
        services.AddSingleton<IBridgeRequestHandler, NUnitRequestHandler>();
        return services;
    }
}
