using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host.Loading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevTools.TUnit.Host;

public static class TUnitHostingExtensions
{
    public static IServiceCollection AddTUnitHostServices(this IServiceCollection services)
    {
        services.TryAddSingleton<TUnitGenerationPolicy>(_ =>
            new TUnitGenerationPolicy(() =>
                HostRuntimeSources.ResolveBesideHost(
                    typeof(TUnitHostingExtensions).Assembly,
                    TUnitGenerationPolicy.RuntimeFolderName,
                    TUnitGenerationPolicy.RuntimeAssemblyFileName)));
        services.TryAddSingleton<TUnitRuntimeSessionFactory>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostTestFrameworkProvider, TUnitHostTestFrameworkProvider>());
        return services;
    }
}
