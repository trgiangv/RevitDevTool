using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Mcp.Catalog;

public sealed class McpCatalogLoader(IEnumerable<IMcpRegistryProvider> providers, ILogger<McpCatalogLoader> logger) : IMcpCatalogLoader
{
    private readonly IReadOnlyList<IMcpRegistryProvider> _providers = providers.ToList();

    public McpRegistryCatalog LoadCatalog(
        IReadOnlyCollection<string> dotnetPaths,
        IReadOnlyCollection<string> pythonPaths)
    {
        ConfigureProviderPaths(dotnetPaths, pythonPaths);

        var toolMap = new Dictionary<string, McpRegisteredTool>(StringComparer.OrdinalIgnoreCase);
        var resourceMap = new Dictionary<string, McpRegisteredResource>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in _providers.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var catalog = provider.LoadCatalog();
                logger.ZLogDebug(
                    $"Provider '{provider.Name}' returned {catalog.Tools.Count} tool(s), {catalog.Resources.Count} resource(s).");

                Collect(provider.Name, catalog.Tools, toolMap, tool => tool.Id, tool => tool.Descriptor.Name, "tool");
                Collect(provider.Name, catalog.Resources, resourceMap, resource => resource.Id,
                    resource => resource.DisplayName, "resource");
            }
            catch (Exception ex)
            {
                logger.ZLogWarning($"Provider '{provider.Name}' failed: {ex.Message}");
            }
        }

        var loaded = new McpRegistryCatalog
        {
            Tools = toolMap.Values
                .OrderBy(tool => tool.Binding.GroupName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(tool => tool.Descriptor.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Resources = resourceMap.Values
                .OrderBy(resource => resource.Binding.GroupName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(resource => resource.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(resource => resource.TemplateDescriptor?.UriTemplate ?? resource.Descriptor?.Uri ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };

        logger.ZLogDebug(
            $"Tool store loaded {loaded.Tools.Count} tool(s), {loaded.Resources.Count} resource(s).");
        return loaded;
    }

    private void ConfigureProviderPaths(IEnumerable<string> dotnetPaths, IEnumerable<string> pythonPaths)
    {
        var resolvedDotnet = McpPathValidator.ResolvePaths(dotnetPaths, McpPathValidator.IsValidDotnetAssemblyPath);
        var resolvedPython = McpPathValidator.ResolvePaths(pythonPaths, McpPathValidator.IsValidPythonToolsetPath);

        var pathsByMode = new Dictionary<ExecutionMode, IReadOnlyList<string>>
        {
            [ExecutionMode.Dotnet] = resolvedDotnet,
            [ExecutionMode.Python] = resolvedPython
        };

        foreach (var provider in _providers)
        {
            if (pathsByMode.TryGetValue(provider.SourceKind, out var paths))
                provider.ConfigurePaths(paths);
        }
    }

    private void Collect<T>(
        string providerName,
        IReadOnlyList<T> items,
        Dictionary<string, T> byId,
        Func<T, string> idSelector,
        Func<T, string> nameSelector,
        string kind)
    {
        foreach (var item in items)
        {
            var id = idSelector(item);
            var name = nameSelector(item);

            if (string.IsNullOrWhiteSpace(name))
            {
                logger.ZLogWarning($"Skip {kind} with empty name from provider='{providerName}'.");
                continue;
            }

            if (byId.TryAdd(id, item)) continue;
            logger.ZLogWarning($"Duplicate {kind} id '{id}' ignored.");
        }
    }
}
