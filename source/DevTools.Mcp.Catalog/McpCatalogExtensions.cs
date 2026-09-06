using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Core;
using DevTools.Mcp.Core.Catalog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Extensions.Tasks;

namespace DevTools.Mcp.Catalog;

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
            services.AddMcpServer().WithTasks(taskStore, options =>
                options.ExecutionModeSelector = McpTaskExecutionMeta.SelectForRequest);
            return services;
        }
        public IServiceCollection AddMcpCatalog()
        {
            services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.TryAddSingleton<McpAssemblyParser>();
            services.TryAddSingleton<PythonToolsetParser>();
            services.TryAddSingleton<DotnetMcpRegistryProvider>();
            services.TryAddSingleton<BuiltInMcpRegistryProvider>();
            services.AddSingleton<IMcpRegistryProvider>(
                sp => sp.GetRequiredService<DotnetMcpRegistryProvider>());
            services.AddSingleton<IMcpRegistryProvider>(
                sp => sp.GetRequiredService<BuiltInMcpRegistryProvider>());
            services.TryAddSingleton<McpCatalogLoader>();
            services.TryAddSingleton<IMcpCatalogLoader>(sp => sp.GetRequiredService<McpCatalogLoader>());
            services.TryAddSingleton<McpCatalogStore>();
            return services;
        }
    }
}
