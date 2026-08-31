using DevTools.Mcp.Core.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Mcp.Client;

/// <summary>Registers host pipe discovery, sessions, and the shared host broker.</summary>
public static class McpHostClientExtensions
{
    public static IServiceCollection AddMcpHostClient(this IServiceCollection services)
    {
        services.AddSingleton<IMcpPipeScanner, McpPipeScanner>();
        services.AddSingleton<HostBroker>();
        services.AddSingleton<IHostBroker>(provider => provider.GetRequiredService<HostBroker>());
        services.AddSingleton<IHostDiscovery>(provider => provider.GetRequiredService<HostBroker>());
        return services;
    }
}
