using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Server.Hosting;

/// <summary>
/// Applies SDK-registered features to manually built <see cref="McpServerOptions"/>.
/// Tasks must configure before ordinary call-tool filters (SDK requirement).
/// </summary>
public static class McpServerConfigurator
{
    public static void Apply(McpServerOptions options, IServiceProvider appServices)
    {
        foreach (var configure in appServices.GetServices<IConfigureOptions<McpServerOptions>>())
            configure.Configure(options);

        McpLogFilters.Attach(options, appServices.GetRequiredService<ILoggerFactory>());
    }
}
