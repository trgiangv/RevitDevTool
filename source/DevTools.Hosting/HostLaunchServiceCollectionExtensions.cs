using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevTools.Hosting;

public static class HostLaunchServiceCollectionExtensions
{
    public static IServiceCollection AddHostLaunchCore(this IServiceCollection services)
    {
        services.TryAddSingleton<HostLaunchService>();
        services.TryAddSingleton<IHostLaunchService>(static sp => sp.GetRequiredService<HostLaunchService>());
        return services;
    }
}
