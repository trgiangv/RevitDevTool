using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Hosting;

public static class McpHostingExtensions
{
    /// <summary>
    /// Registers the in-host named-pipe MCP server host and options factory.
    /// Call after tool/prompt/resource collections are registered on the same container.
    /// </summary>
    public static IServiceCollection AddHostMcpPipeServer(this IServiceCollection services)
    {
        services.AddSingleton<HostMcpServerOptionsFactory>();
        services.AddSingleton<HostMcpServerHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<HostMcpServerHostedService>());
        return services;
    }
}
