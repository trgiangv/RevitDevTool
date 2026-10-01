using DevTools.Ipc;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.NUnit;
using DevTools.Testing.Host.Runtime;
using DevTools.Testing.Host.TUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevTools.Testing.Host;

public static class TestingHostingExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers first-party in-host providers (NUnit, TUnit) and the
        /// <c>testing/*</c> bridge handler. Call from Revit/AutoCAD hosting after
        /// <c>AddExecutionServices()</c>.
        /// </summary>
        public IServiceCollection AddTestingHostServices()
        {
            services.AddNUnitHostServices();
            services.AddTUnitHostServices();
            return services.AddGenericTestingHostServices();
        }
        
        public IServiceCollection AddGenericTestingHostServices()
        {
            services.TryAddSingleton<TestingProviderRegistry>();
            services.AddSingleton<IBridgeRequestHandler, MarshaledTestRequestHandler>();
            return services;
        }
        
        public IServiceCollection AddNUnitHostServices()
        {
            services.TryAddSingleton<NUnitGenerationPolicy>(_ =>
                new NUnitGenerationPolicy(() =>
                    RuntimeSource.ResolveBeside(
                        typeof(TestingHostingExtensions).Assembly,
                        NUnitGenerationPolicy.RuntimeFolderName,
                        NUnitGenerationPolicy.RuntimeAssemblyFileName,
                        NUnitGenerationPolicy.RuntimeSymbolFileName)));
            AddFrameworkProvider<NUnitGenerationPolicy>(
                services,
                policy => new FrameworkProvider(
                    policy,
                    new NUnitRuntimeSessionFactory()));
            return services;
        }
        
        public IServiceCollection AddTUnitHostServices()
        {
            services.TryAddSingleton<TUnitGenerationPolicy>(_ =>
                new TUnitGenerationPolicy(() =>
                    RuntimeSource.ResolveBeside(
                        typeof(TestingHostingExtensions).Assembly,
                        TUnitGenerationPolicy.RuntimeFolderName,
                        TUnitGenerationPolicy.RuntimeAssemblyFileName)));
            AddFrameworkProvider<TUnitGenerationPolicy>(
                services,
                policy => new FrameworkProvider(
                    policy,
                    new ManifestRuntimeSessionFactory(TUnitGenerationPolicy.RuntimeSessionTypeName)));
            return services;
        }
    }

    private static void AddFrameworkProvider<TPolicy>(
        IServiceCollection services,
        Func<TPolicy, ITestFrameworkProvider> create)
        where TPolicy : class, ITestingGenerationPolicy
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(ITestFrameworkProvider)
                && descriptor.ImplementationFactory?.Target is FrameworkRegistration existing
                && existing.PolicyType == typeof(TPolicy))
            {
                return;
            }
        }

        var registration = new FrameworkRegistration(
            typeof(TPolicy),
            provider => create(provider.GetRequiredService<TPolicy>()));
        services.AddSingleton<ITestFrameworkProvider>(registration.Create);
    }

    private sealed class FrameworkRegistration(Type policyType, Func<IServiceProvider, ITestFrameworkProvider> create)
    {
        public Type PolicyType { get; } = policyType;

        public ITestFrameworkProvider Create(IServiceProvider services) => create(services);
    }
}
