using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Testing.Host.Tests;

[TestClass]
public sealed class ProviderHostingTests
{
    [TestMethod]
    public void Provider_descriptor_count_is_independent_of_registration_order()
    {
        var nunitFirst = CountProviderDescriptors(services =>
        {
            services.AddNUnitHostServices();
            services.AddTUnitHostServices();
        });
        var tunitFirst = CountProviderDescriptors(services =>
        {
            services.AddTUnitHostServices();
            services.AddNUnitHostServices();
        });

        Assert.AreEqual(2, nunitFirst);
        Assert.AreEqual(2, tunitFirst);
    }

    [TestMethod]
    public void Acad_style_registration_resolves_both_providers()
    {
        var services = new ServiceCollection();
        services.AddTestingHostServices();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<TestingProviderRegistry>();

        Assert.AreEqual(TestFrameworkId.NUnit, registry.GetRequired(TestFrameworkId.NUnit).FrameworkId);
        Assert.AreEqual(TestFrameworkId.TUnit, registry.GetRequired(TestFrameworkId.TUnit).FrameworkId);
    }

    [TestMethod]
    public void Revit_style_registration_resolves_both_providers()
    {
        var services = new ServiceCollection();
        services.AddTestingHostServices();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<TestingProviderRegistry>();

        Assert.AreEqual(TestFrameworkId.NUnit, registry.GetRequired(TestFrameworkId.NUnit).FrameworkId);
        Assert.AreEqual(TestFrameworkId.TUnit, registry.GetRequired(TestFrameworkId.TUnit).FrameworkId);
    }

    [TestMethod]
    public void Provider_registration_does_not_expose_unkeyed_kernel_singletons()
    {
        var services = new ServiceCollection();
        services.AddNUnitHostServices();
        services.AddTUnitHostServices();

        Assert.IsFalse(services.Any(descriptor => descriptor.ServiceType == typeof(TestingGenerationStore)));
        Assert.IsFalse(services.Any(descriptor => descriptor.ServiceType == typeof(ITestingGenerationPolicy)));
        Assert.IsFalse(services.Any(descriptor => descriptor.ServiceType == typeof(TestingRuntimeSessionManager)));
        Assert.IsFalse(services.Any(descriptor => descriptor.ServiceType == typeof(ITestingRuntimeSessionFactory)));
    }

    private static int CountProviderDescriptors(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.Count(descriptor => descriptor.ServiceType == typeof(ITestFrameworkProvider));
    }
}
