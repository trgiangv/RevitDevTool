using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Catalog;
using DevTools.Mcp.Core.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Mcp.Catalog;

public sealed class McpCatalogLoader(IEnumerable<IMcpRegistryProvider> providers, ILogger<McpCatalogLoader> logger) : IMcpCatalogLoader
{
    private readonly IReadOnlyList<IMcpRegistryProvider> _providers = providers.ToList();
    private readonly HashSet<string> _knownToolIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownResourceIds = new(StringComparer.OrdinalIgnoreCase);

    public McpRegistryCatalog LoadCatalog(
        IReadOnlyCollection<string> dotnetPaths,
        IReadOnlyCollection<string> pythonPaths)
    {
        ConfigureProviderPaths(dotnetPaths, pythonPaths);

        var toolMap = new Dictionary<string, McpRegisteredTool>(StringComparer.OrdinalIgnoreCase);
        var resourceMap = new Dictionary<string, McpRegisteredResource>(StringComparer.OrdinalIgnoreCase);
        var toolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resourceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in _providers.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var catalog = provider.LoadCatalog();
                var addedTools = CountUnknown(catalog.Tools, tool => tool.Id, _knownToolIds);
                var addedResources = CountUnknown(catalog.Resources, resource => resource.Id, _knownResourceIds);
                if (addedTools > 0 || addedResources > 0)
                {
                    logger.ZLogDebug(
                        $"Provider '{provider.Name}' added {addedTools} tool(s), {addedResources} resource(s).");
                }

                Collect(provider.Name, catalog.Tools, toolMap, toolNames, tool => tool.Id, tool => tool.Descriptor.Name, "tool");
                Collect(provider.Name, catalog.Resources, resourceMap, resourceNames,
                    resource => resource.Id, resource => resource.DisplayName, "resource");
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

        var newToolCount = CountUnknown(loaded.Tools, tool => tool.Id, _knownToolIds);
        var newResourceCount = CountUnknown(loaded.Resources, resource => resource.Id, _knownResourceIds);
        if (newToolCount > 0 || newResourceCount > 0)
        {
            logger.ZLogDebug(
                $"Tool store added {newToolCount} tool(s), {newResourceCount} resource(s) (total {loaded.Tools.Count} tools, {loaded.Resources.Count} resources).");
        }

        ReplaceKnownIds(_knownToolIds, loaded.Tools.Select(tool => tool.Id));
        ReplaceKnownIds(_knownResourceIds, loaded.Resources.Select(resource => resource.Id));
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
        HashSet<string> names,
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

            if (!byId.TryAdd(id, item))
            {
                logger.ZLogWarning($"Duplicate {kind} id '{id}' ignored.");
                continue;
            }

            if (names.Add(name)) continue;

            byId.Remove(id);
            logger.ZLogWarning($"Duplicate {kind} name '{name}' ignored.");
        }
    }

    private static int CountUnknown<T>(IReadOnlyList<T> items, Func<T, string> idSelector, HashSet<string> known)
    {
        var added = 0;
        foreach (var item in items)
        {
            var id = idSelector(item);
            if (!string.IsNullOrWhiteSpace(id) && !known.Contains(id))
                added++;
        }

        return added;
    }

    private static void ReplaceKnownIds(HashSet<string> known, IEnumerable<string> ids)
    {
        known.Clear();
        foreach (var id in ids)
        {
            if (!string.IsNullOrWhiteSpace(id))
                known.Add(id);
        }
    }
}
