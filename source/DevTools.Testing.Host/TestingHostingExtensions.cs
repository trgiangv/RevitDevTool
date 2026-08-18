using DevTools.Ipc;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Testing.Host;

public static class TestingHostingExtensions
{
    public static IServiceCollection AddGenericTestingHostServices(this IServiceCollection services)
    {
        services.AddSingleton<IBridgeRequestHandler, MarshaledTestingRequestHandler>();
        return services;
    }
}
