using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Catalog;
using DevTools.Mcp.Core.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Mcp;

public sealed class McpCatalogLoader(IEnumerable<IRegistryProvider> providers, ILogger<McpCatalogLoader> logger) : ICatalogLoader
{
    private readonly IReadOnlyList<IRegistryProvider> _providers = providers.ToList();
    private readonly HashSet<string> _knownToolIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownResourceIds = new(StringComparer.OrdinalIgnoreCase);

    public RegistryCatalog LoadCatalog(
        IReadOnlyCollection<string> dotnetPaths,
        IReadOnlyCollection<string> pythonPaths)
    {
        ConfigureProviderPaths(dotnetPaths, pythonPaths);
        var toolMap = new Dictionary<string, RegisteredTool>(StringComparer.OrdinalIgnoreCase);
        var resourceMap = new Dictionary<string, RegisteredResource>(StringComparer.OrdinalIgnoreCase);
        var toolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resourceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        LoadProviders(toolMap, resourceMap, toolNames, resourceNames);

        var loaded = CreateCatalog(toolMap, resourceMap);
        UpdateKnownIds(loaded);
        return loaded;
    }

    private void LoadProviders(
        Dictionary<string, RegisteredTool> toolMap,
        Dictionary<string, RegisteredResource> resourceMap,
        HashSet<string> toolNames,
        HashSet<string> resourceNames)
    {
        foreach (var provider in _providers.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            LoadProvider(provider, toolMap, resourceMap, toolNames, resourceNames);
    }

    private void LoadProvider(
        IRegistryProvider provider,
        Dictionary<string, RegisteredTool> toolMap,
        Dictionary<string, RegisteredResource> resourceMap,
        HashSet<string> toolNames,
        HashSet<string> resourceNames)
    {
        try
        {
            var catalog = provider.LoadCatalog();
            LogProviderAdditions(provider.Name, catalog);
            Collect(provider.Name, catalog.Tools, toolMap, toolNames, tool => tool.Id, tool => tool.Descriptor.Name, "tool");
            Collect(provider.Name, catalog.Resources, resourceMap, resourceNames,
                resource => resource.Id, resource => resource.DisplayName, "resource");
        }
        catch (Exception ex)
        {
            logger.ZLogWarning($"Provider '{provider.Name}' failed: {ex.Message}");
        }
    }

    private void LogProviderAdditions(string providerName, RegistryCatalog catalog)
    {
        var addedTools = CountUnknown(catalog.Tools, tool => tool.Id, _knownToolIds);
        var addedResources = CountUnknown(catalog.Resources, resource => resource.Id, _knownResourceIds);
        if (addedTools == 0 && addedResources == 0) return;

        logger.ZLogDebug($"Provider '{providerName}' added {addedTools} tool(s), {addedResources} resource(s).");
    }

    private static RegistryCatalog CreateCatalog(
        Dictionary<string, RegisteredTool> toolMap,
        Dictionary<string, RegisteredResource> resourceMap) => new()
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

    private void UpdateKnownIds(RegistryCatalog loaded)
    {
        LogStoreAdditions(loaded);
        ReplaceKnownIds(_knownToolIds, loaded.Tools.Select(tool => tool.Id));
        ReplaceKnownIds(_knownResourceIds, loaded.Resources.Select(resource => resource.Id));
    }

    private void LogStoreAdditions(RegistryCatalog loaded)
    {
        var newToolCount = CountUnknown(loaded.Tools, tool => tool.Id, _knownToolIds);
        var newResourceCount = CountUnknown(loaded.Resources, resource => resource.Id, _knownResourceIds);
        if (newToolCount == 0 && newResourceCount == 0) return;

        logger.ZLogDebug(
            $"Tool store added {newToolCount} tool(s), {newResourceCount} resource(s) (total {loaded.Tools.Count} tools, {loaded.Resources.Count} resources).");
    }

    private void ConfigureProviderPaths(IEnumerable<string> dotnetPaths, IEnumerable<string> pythonPaths)
    {
        var resolvedDotnet = McpPathValidator.ResolvePaths(dotnetPaths, McpPathValidator.IsValidAssemblyPath);
        var resolvedPython = McpPathValidator.ResolvePaths(pythonPaths, McpPathValidator.IsValidPythonPath);

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
