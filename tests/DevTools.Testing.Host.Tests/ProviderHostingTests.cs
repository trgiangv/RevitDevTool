using DevTools.NUnit.Host;
using DevTools.TUnit.Host;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Host;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Testing.Host.Tests;

public sealed class ProviderHostingTests
{
    [Fact]
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

        Assert.Equal(2, nunitFirst);
        Assert.Equal(2, tunitFirst);
    }

    [Fact]
    public void Acad_style_registration_resolves_both_providers()
    {
        var services = new ServiceCollection();
        services.AddNUnitHostServices();
        services.AddTUnitHostServices();
        services.AddGenericTestingHostServices();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<TestingProviderRegistry>();

        Assert.Equal("nunit", registry.GetRequired("nunit").FrameworkId);
        Assert.Equal("tunit", registry.GetRequired("tunit").FrameworkId);
    }

    [Fact]
    public void Revit_style_registration_resolves_both_providers()
    {
        var services = new ServiceCollection();
        services.AddNUnitHostServices();
        services.AddTUnitHostServices();
        services.AddGenericTestingHostServices();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<TestingProviderRegistry>();

        Assert.Equal("nunit", registry.GetRequired("nunit").FrameworkId);
        Assert.Equal("tunit", registry.GetRequired("tunit").FrameworkId);
    }

    [Fact]
    public void Provider_registration_does_not_expose_unkeyed_kernel_singletons()
    {
        var services = new ServiceCollection();
        services.AddNUnitHostServices();
        services.AddTUnitHostServices();

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(TestingGenerationStore));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(ITestingGenerationPolicy));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(TestingRuntimeSessionManager));
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(ITestingRuntimeSessionFactory));
    }

    private static int CountProviderDescriptors(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.Count(descriptor => descriptor.ServiceType == typeof(IHostTestFrameworkProvider));
    }
}
