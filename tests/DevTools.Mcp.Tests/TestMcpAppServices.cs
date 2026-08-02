using DevTools.Mcp.Catalog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
namespace DevTools.Mcp.Tests;

internal static class TestMcpAppServices
{
    public static ServiceProvider Create(ILoggerFactory? loggerFactory = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory ?? NullLoggerFactory.Instance);
        services.AddMcp();
        return services.BuildServiceProvider();
    }
}
