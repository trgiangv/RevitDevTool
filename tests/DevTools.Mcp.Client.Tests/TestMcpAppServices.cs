using DevTools.Daemon.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Mcp.Client.Tests;

internal static class TestMcpAppServices
{
    public static ServiceProvider Create(ILoggerFactory? loggerFactory = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory ?? NullLoggerFactory.Instance);
        McpServiceCollectionExtensions.AddMcp(services);
        return services.BuildServiceProvider();
    }
}
