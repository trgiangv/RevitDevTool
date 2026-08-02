using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Extensions.Tasks;

namespace DevTools.Mcp.Catalog;

public static class McpCatalogExtensions
{
    /// <summary>Registers shared MCP SDK features on the application container.</summary>
    public static IServiceCollection AddMcp(this IServiceCollection services)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        var taskStore = new InMemoryMcpTaskStore();
        services.AddSingleton<IMcpTaskStore>(taskStore);
        services.AddMcpServer().WithTasks(taskStore, options =>
            options.ExecutionModeSelector = McpTaskExecutionMeta.SelectForRequest);
        return services;
    }

    public static IServiceCollection AddMcpCatalog(this IServiceCollection services)
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
        services.TryAddSingleton<IHostPrimitiveRegistry>(sp => sp.GetRequiredService<McpCatalogStore>());
        return services;
    }
}
