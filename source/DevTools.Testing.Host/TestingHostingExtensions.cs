using DevTools.Ipc;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.NUnit;
using DevTools.Testing.Host.NUnit.Loading;
using DevTools.Testing.Host.TUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevTools.Testing.Host;

public static class TestingHostingExtensions
{
    /// <summary>
    /// Registers first-party in-host providers (NUnit, TUnit) and the
    /// <c>testing/*</c> bridge handler. Call from Revit/AutoCAD hosting after
    /// <c>AddExecutionServices()</c>.
    /// </summary>
    public static IServiceCollection AddTestingHostServices(this IServiceCollection services)
    {
        AddNUnitHostServices(services);
        AddTUnitHostServices(services);
        return AddGenericTestingHostServices(services);
    }

    public static IServiceCollection AddGenericTestingHostServices(this IServiceCollection services)
    {
        services.TryAddSingleton<TestingProviderRegistry>();
        services.AddSingleton<IBridgeRequestHandler, MarshaledTestRequestHandler>();
        return services;
    }

    public static IServiceCollection AddNUnitHostServices(this IServiceCollection services)
    {
        services.TryAddSingleton<NUnitGenerationPolicy>(_ =>
            new NUnitGenerationPolicy(() =>
                HostRuntimeSources.ResolveBesideHost(
                    typeof(TestingHostingExtensions).Assembly,
                    NUnitGenerationPolicy.RuntimeFolderName,
                    NUnitGenerationPolicy.RuntimeAssemblyFileName,
                    NUnitGenerationPolicy.RuntimeSymbolFileName)));
        services.TryAddSingleton<NUnitRuntimeSessionFactory>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ITestFrameworkProvider, NUnitTestFrameworkProvider>());
        return services;
    }

    public static IServiceCollection AddTUnitHostServices(this IServiceCollection services)
    {
        services.TryAddSingleton<TUnitGenerationPolicy>(_ =>
            new TUnitGenerationPolicy(() =>
                HostRuntimeSources.ResolveBesideHost(
                    typeof(TestingHostingExtensions).Assembly,
                    TUnitGenerationPolicy.RuntimeFolderName,
                    TUnitGenerationPolicy.RuntimeAssemblyFileName)));
        services.TryAddSingleton<TUnitRuntimeSessionFactory>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ITestFrameworkProvider, TUnitTestFrameworkProvider>());
        return services;
    }
}
