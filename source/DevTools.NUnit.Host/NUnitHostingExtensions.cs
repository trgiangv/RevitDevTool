using DevTools.NUnit.Host.Loading;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host.Loading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevTools.NUnit.Host;

public static class NUnitHostingExtensions
{
    /// <summary>
    /// Registers the native NUnit runtime manager, generation policy, NUnit
    /// provider, and neutral <c>testing/*</c> bridge handler. Call from Revit/AutoCAD
    /// hosting after <c>AddExecutionServices()</c>.
    /// </summary>
    /// <remarks>
    /// <c>testing/*</c> is registered separately by <see cref="AddGenericTestingHostServices"/>.
    /// </remarks>
    public static IServiceCollection AddNUnitHostServices(this IServiceCollection services)
    {
        services.TryAddSingleton<NUnitGenerationPolicy>(_ =>
            new NUnitGenerationPolicy(() =>
                HostRuntimeSources.ResolveBesideHost(
                    typeof(NUnitHostingExtensions).Assembly,
                    NUnitGenerationPolicy.RuntimeFolderName,
                    NUnitGenerationPolicy.RuntimeAssemblyFileName,
                    NUnitGenerationPolicy.RuntimeSymbolFileName)));
        services.TryAddSingleton<NUnitRuntimeSessionFactory>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostTestFrameworkProvider, NUnitHostTestFrameworkProvider>());
        return services;
    }
}
