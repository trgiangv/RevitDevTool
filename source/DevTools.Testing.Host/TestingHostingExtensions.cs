using DevTools.Hosting;
using DevTools.Ipc;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.NUnit;
using DevTools.Testing.Host.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Testing.Host;

public static class TestingHostingExtensions
{
    private const string UnknownHostName = "The host";

    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers first-party in-host providers (NUnit, TUnit, MSTest) and the
        /// <c>testing/*</c> bridge handler. Call from Revit/AutoCAD hosting after
        /// <c>AddExecutionServices()</c>.
        /// </summary>
        public IServiceCollection AddTestingHostServices()
        {
            services.AddNUnitHostServices();
            services.AddTUnitHostServices();
            services.AddMsTestHostServices();
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
            AddFrameworkProvider(
                services,
                TestFrameworkId.NUnit,
                provider =>
                {
                    var hostName = provider.GetService<IHostAppInfo>()?.Host.ProductName() ?? UnknownHostName;
                    return new OutputSessionFactory(
                        TestFrameworkId.NUnit,
                        TestingLogger(provider),
                        frameworkPath => NUnitFrameworkHostShare.GetOrLoadFromShadow(frameworkPath, hostName));
                });
            return services;
        }

        public IServiceCollection AddTUnitHostServices()
        {
            AddFrameworkProvider(
                services,
                TestFrameworkId.TUnit,
                provider => new OutputSessionFactory(TestFrameworkId.TUnit, TestingLogger(provider)));
            return services;
        }

        public IServiceCollection AddMsTestHostServices()
        {
            AddFrameworkProvider(
                services,
                TestFrameworkId.MSTest,
                provider => new OutputSessionFactory(TestFrameworkId.MSTest, TestingLogger(provider)));
            return services;
        }
    }

    private static ILogger TestingLogger(IServiceProvider provider) =>
        provider.GetService<ILoggerFactory>()?.CreateLogger("DevTools.Testing.Host") ?? NullLogger.Instance;

    private static void AddFrameworkProvider(
        IServiceCollection services,
        TestFrameworkId frameworkId,
        Func<IServiceProvider, ITestingRuntimeSessionFactory> createFactory)
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(ITestFrameworkProvider)
                && descriptor.ImplementationFactory?.Target is FrameworkRegistration existing
                && existing.FrameworkId == frameworkId)
            {
                return;
            }
        }

        var registration = new FrameworkRegistration(
            frameworkId,
            provider => new FrameworkProvider(new OutputGeneration(frameworkId), createFactory(provider)));
        services.AddSingleton<ITestFrameworkProvider>(registration.Create);
    }

    private sealed class FrameworkRegistration(TestFrameworkId frameworkId, Func<IServiceProvider, ITestFrameworkProvider> create)
    {
        public TestFrameworkId FrameworkId { get; } = frameworkId;

        public ITestFrameworkProvider Create(IServiceProvider services) => create(services);
    }
}
