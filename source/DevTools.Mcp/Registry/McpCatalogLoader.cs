using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Mcp.Registry;

public sealed class McpCatalogLoader(IEnumerable<IMcpRegistryProvider> providers, ILogger<McpCatalogLoader> logger)
{
    private readonly IReadOnlyList<IMcpRegistryProvider> _providers = providers.ToList();

    public McpRegistryCatalog LoadCatalog(
        IEnumerable<string> dotnetPaths,
        IEnumerable<string> pythonPaths)
    {
        ConfigureProviderPaths(dotnetPaths, pythonPaths);

        var toolMap = new Dictionary<string, McpRegisteredTool>(StringComparer.OrdinalIgnoreCase);
        var promptMap = new Dictionary<string, McpRegisteredPrompt>(StringComparer.OrdinalIgnoreCase);
        var resourceMap = new Dictionary<string, McpRegisteredResource>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in _providers.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var catalog = provider.LoadCatalog();
                logger.ZLogDebug(
                    $"[MCP] Provider '{provider.Name}' returned {catalog.Tools.Count} tool(s), {catalog.Prompts.Count} prompt(s), {catalog.Resources.Count} resource(s).");

                Collect(provider.Name, catalog.Tools, toolMap, tool => tool.Id, tool => tool.ProtocolTool.Name, "tool");
                Collect(provider.Name, catalog.Prompts, promptMap, prompt => prompt.Id, prompt => prompt.ProtocolPrompt.Name, "prompt");
                Collect(provider.Name, catalog.Resources, resourceMap, resource => resource.Id,
                    resource => resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name ?? string.Empty, "resource");
            }
            catch (Exception ex)
            {
                logger.ZLogWarning($"[MCP] Provider '{provider.Name}' failed: {ex.Message}");
            }
        }

        var loaded = new McpRegistryCatalog
        {
            Tools = toolMap.Values
                .OrderBy(tool => tool.Binding.GroupName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(tool => tool.ProtocolTool.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Prompts = promptMap.Values
                .OrderBy(prompt => prompt.Binding.GroupName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(prompt => prompt.ProtocolPrompt.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Resources = resourceMap.Values
                .OrderBy(resource => resource.Binding.GroupName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(resource => resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(resource => resource.ProtocolTemplate?.UriTemplate ?? resource.ProtocolResource?.Uri ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };

        logger.ZLogDebug(
            $"[MCP] Tool store loaded {loaded.Tools.Count} tool(s), {loaded.Prompts.Count} prompt(s), {loaded.Resources.Count} resource(s).");
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
                logger.ZLogWarning($"[MCP] Skip {kind} with empty name from provider='{providerName}'.");
                continue;
            }

            if (byId.TryAdd(id, item)) continue;
            logger.ZLogWarning($"[MCP] Duplicate {kind} id '{id}' ignored.");
        }
    }
}
