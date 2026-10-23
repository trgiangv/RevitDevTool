using DevTools.Mcp.Core.Catalog;
using DevTools.Mcp.Discovery;
using DevTools.Mcp.Providers;
using DevTools.Mcp.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Extensions.Tasks;

namespace DevTools.Mcp.Hosting;

public static class McpCatalogExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers shared MCP SDK features on the application container.</summary>
        public IServiceCollection AddMcp()
        {
            ArgumentNullException.ThrowIfNull(services);

            var taskStore = new InMemoryMcpTaskStore();
            services.AddSingleton<IMcpTaskStore>(taskStore);
            services.AddMcpServer().WithTasks(taskStore);
            return services;
        }

        public IServiceCollection AddMcpCatalog()
        {
            services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.TryAddSingleton<MethodLookup>();
            services.TryAddSingleton<McpAssemblyParser>();
            services.TryAddSingleton<McpPythonParser>();
            services.TryAddSingleton<DotnetMcpRegistry>();
            services.TryAddSingleton<BuiltInMcpRegistry>();
            services.AddSingleton<IRegistryProvider>(
                sp => sp.GetRequiredService<DotnetMcpRegistry>());
            services.AddSingleton<IRegistryProvider>(
                sp => sp.GetRequiredService<BuiltInMcpRegistry>());
            services.TryAddSingleton<McpCatalogLoader>();
            services.TryAddSingleton<ICatalogLoader>(sp => sp.GetRequiredService<McpCatalogLoader>());
            services.TryAddSingleton<McpCatalogStore>();
            return services;
        }

        /// <summary>Registers the in-host named-pipe server.</summary>
        public IServiceCollection AddMcpAdapter()
        {
            services.AddSingleton<McpPipeServer>();
            services.AddHostedService(sp => sp.GetRequiredService<McpPipeServer>());
            return services;
        }
    }
}
