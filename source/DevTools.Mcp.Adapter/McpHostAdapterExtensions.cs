using DevTools.Mcp.Adapter.External;
using DevTools.Mcp.Adapter.Host;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevTools.Mcp.Adapter;

/// <summary>Registers the in-host MCP adapter and named-pipe transport.</summary>
public static class McpHostAdapterExtensions
{
    public static IServiceCollection AddMcpHostAdapter(this IServiceCollection services)
    {
        services.TryAddSingleton<IMcpHandler, McpHandler>();
        services.AddSingleton<HostMcpPipeServer>();
        services.AddHostedService(sp => sp.GetRequiredService<HostMcpPipeServer>());
        return services;
    }
}
