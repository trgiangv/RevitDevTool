using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Hosting;

/// <summary>Applies SDK-registered features to manually built <see cref="McpServerOptions"/>.</summary>
public static class McpServerConfigurator
{
    public static void Apply(McpServerOptions options, IServiceProvider appServices)
    {
        foreach (var configure in appServices.GetServices<IConfigureOptions<McpServerOptions>>())
            configure.Configure(options);

        McpCacheFilters.Attach(options);
        McpLogFilters.Attach(options, appServices.GetRequiredService<ILoggerFactory>());
    }
}
