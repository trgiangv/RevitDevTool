using DevTools.Daemon.Mcp.Hosting;
using DevTools.Daemon.Mcp.Processes;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Extensions.Tasks;

namespace DevTools.Daemon.Mcp;

public static class McpServicesExtensions
{
    public static IServiceCollection AddMcp(this IServiceCollection services)
    {
        var taskStore = new InMemoryMcpTaskStore
        {
            DefaultTimeToLive = TimeSpan.FromMinutes(30),
            DefaultPollIntervalMs = 1000,
        };
        services.AddSingleton<IMcpTaskStore>(taskStore);
        services.AddMcpServer().WithTasks(taskStore, options =>
            options.ExecutionModeSelector = TaskSelection.Select);

        services.AddSingleton<IMcpPipeScanner, McpPipeScanner>();
        services.AddSingleton<HostConnections>();
        services.AddSingleton<IHostConnections>(sp => sp.GetRequiredService<HostConnections>());
        services.AddSingleton<McpEngine>();
        return services;
    }
}
