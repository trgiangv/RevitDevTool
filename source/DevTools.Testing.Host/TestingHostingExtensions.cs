using DevTools.Ipc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevTools.Testing.Host;

public static class TestingHostingExtensions
{
    public static IServiceCollection AddGenericTestingHostServices(this IServiceCollection services)
    {
        services.TryAddSingleton<TestingProviderRegistry>();
        services.AddSingleton<IBridgeRequestHandler, MarshaledTestRequestHandler>();
        return services;
    }
}
