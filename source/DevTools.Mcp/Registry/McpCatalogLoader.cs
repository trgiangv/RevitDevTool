using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ZLogger;

namespace DevTools.Mcp.Registry;

public sealed class McpCatalogLoader(
    IEnumerable<IMcpRegistryProvider> providers,
    ILogger<McpCatalogLoader> logger,
    IEnumerable<IMcpServerPrimitiveAdapter>? primitiveAdapters = null)
{
    private readonly IReadOnlyList<IMcpRegistryProvider> _providers = providers.ToList();
    private readonly IReadOnlyDictionary<ExecutionMode, IMcpServerPrimitiveAdapter> _adapters =
        (primitiveAdapters ?? []).GroupBy(adapter => adapter.SourceKind).ToDictionary(group => group.Key, group => group.First());

    public McpCatalogLoadResult LoadCatalog(
        IEnumerable<string> dotnetPaths,
        IEnumerable<string> pythonPaths)
    {
        ConfigureProviderPaths(dotnetPaths, pythonPaths);

        var diagnostics = new List<McpCatalogDiagnostic>();
        var toolMap = new Dictionary<string, McpRegisteredTool>(StringComparer.OrdinalIgnoreCase);
        var promptMap = new Dictionary<string, McpRegisteredPrompt>(StringComparer.OrdinalIgnoreCase);
        var resources = new List<McpRegisteredResource>();
        McpServerResourceCollection resourceIdentities = [];

        foreach (var provider in _providers.OrderBy(provider => provider.Priority).ThenBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var providerCatalog = provider.LoadCatalog();
                logger.ZLogDebug($"Provider '{provider.Name}' returned {providerCatalog.Tools.Count} tool(s), {providerCatalog.Prompts.Count} prompt(s), {providerCatalog.Resources.Count} resource(s).");

                Collect(provider, providerCatalog.Tools, toolMap, tool => tool.ProtocolTool.Name, "tool", diagnostics);
                Collect(provider, providerCatalog.Prompts, promptMap, prompt => prompt.ProtocolPrompt.Name, "prompt", diagnostics);
                CollectResources(provider, providerCatalog.Resources, resources, resourceIdentities, diagnostics);
            }
            catch (Exception ex)
            {
                diagnostics.Add(new McpCatalogDiagnostic("provider_load_failed", "catalog", string.Empty, provider.Name, ex.Message));
                logger.ZLogWarning($"Provider '{provider.Name}' failed: {ex.Message}");
            }
        }

        var catalog = new McpRegistryCatalog
        {
            Tools = toolMap.Values.OrderBy(tool => tool.Binding.GroupName, StringComparer.OrdinalIgnoreCase).ThenBy(tool => tool.ProtocolTool.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            Prompts = promptMap.Values.OrderBy(prompt => prompt.Binding.GroupName, StringComparer.OrdinalIgnoreCase).ThenBy(prompt => prompt.ProtocolPrompt.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            Resources = resources.OrderBy(resource => resource.Binding.GroupName, StringComparer.OrdinalIgnoreCase).ThenBy(ResourceKey, StringComparer.Ordinal).ToList()
        };

        var snapshot = BuildSnapshot(catalog, diagnostics);
        logger.ZLogDebug($"Tool store loaded {catalog.Tools.Count} tool(s), {catalog.Prompts.Count} prompt(s), {catalog.Resources.Count} resource(s).");
        return new McpCatalogLoadResult(catalog, snapshot);
    }

    private McpPrimitiveSnapshot BuildSnapshot(McpRegistryCatalog catalog, List<McpCatalogDiagnostic> diagnostics)
    {
        var tools = BuildPrimitives(catalog.Tools, "tool", tool => tool.ProtocolTool.Name, tool => ResolveAdapter(tool.Binding.SourceKind)?.CreateTool(tool), diagnostics);
        var prompts = BuildPrimitives(catalog.Prompts, "prompt", prompt => prompt.ProtocolPrompt.Name, prompt => ResolveAdapter(prompt.Binding.SourceKind)?.CreatePrompt(prompt), diagnostics);
        var resources = BuildPrimitives(catalog.Resources, "resource", ResourceKey, resource => ResolveAdapter(resource.Binding.SourceKind)?.CreateResource(resource), diagnostics);
        return new McpPrimitiveSnapshot(tools, prompts, resources, diagnostics.ToList());
    }

    private List<TPrimitive> BuildPrimitives<TRegistered, TPrimitive>(
        IEnumerable<TRegistered> registrations,
        string kind,
        Func<TRegistered, string> keySelector,
        Func<TRegistered, TPrimitive?> create,
        List<McpCatalogDiagnostic> diagnostics)
        where TRegistered : class
        where TPrimitive : class
    {
        var primitives = new List<TPrimitive>();
        foreach (var registration in registrations)
        {
            var primitive = create(registration);
            if (primitive is not null)
            {
                primitives.Add(primitive);
                continue;
            }

            var binding = registration switch
            {
                McpRegisteredTool tool => tool.Binding,
                McpRegisteredPrompt prompt => prompt.Binding,
                McpRegisteredResource resource => resource.Binding,
                _ => throw new InvalidOperationException($"Unsupported MCP registration '{typeof(TRegistered).Name}'.")
            };
            diagnostics.Add(new McpCatalogDiagnostic("primitive_adapter_unavailable", kind, keySelector(registration), binding.SourceKind.ToString(),
                $"No SDK primitive adapter accepted {kind} '{keySelector(registration)}' from {binding.SourceKind}."));
        }

        return primitives;
    }

    private IMcpServerPrimitiveAdapter? ResolveAdapter(ExecutionMode sourceKind) =>
        _adapters.TryGetValue(sourceKind, out var adapter) ? adapter : null;

    private void ConfigureProviderPaths(IEnumerable<string> dotnetPaths, IEnumerable<string> pythonPaths)
    {
        var pathsByMode = new Dictionary<ExecutionMode, IReadOnlyList<string>>
        {
            [ExecutionMode.Dotnet] = McpPathValidator.ResolvePaths(dotnetPaths, McpPathValidator.IsValidDotnetAssemblyPath),
            [ExecutionMode.Python] = McpPathValidator.ResolvePaths(pythonPaths, McpPathValidator.IsValidPythonToolsetPath)
        };

        foreach (var provider in _providers)
            if (pathsByMode.TryGetValue(provider.SourceKind, out var paths))
                provider.ConfigurePaths(paths);
    }

    private void Collect<T>(IMcpRegistryProvider provider, IEnumerable<T> items, Dictionary<string, T> byKey,
        Func<T, string> keySelector, string kind, List<McpCatalogDiagnostic> diagnostics)
    {
        foreach (var item in items)
        {
            var key = keySelector(item);
            if (string.IsNullOrWhiteSpace(key))
            {
                diagnostics.Add(new McpCatalogDiagnostic("invalid_primitive", kind, string.Empty, provider.Name, $"{kind} has an empty protocol identity."));
                logger.ZLogWarning($"Skip {kind} with empty identity from provider='{provider.Name}'.");
                continue;
            }

            if (byKey.TryAdd(key, item))
                continue;

            diagnostics.Add(new McpCatalogDiagnostic("duplicate_primitive", kind, key, provider.Name,
                $"{kind} '{key}' was rejected because an earlier provider reserved the protocol identity."));
            logger.ZLogWarning($"Duplicate {kind} protocol identity '{key}' ignored from provider '{provider.Name}'.");
        }
    }

    private void CollectResources(
        IMcpRegistryProvider provider,
        IEnumerable<McpRegisteredResource> items,
        List<McpRegisteredResource> accepted,
        McpServerResourceCollection identities,
        List<McpCatalogDiagnostic> diagnostics)
    {
        foreach (var item in items)
        {
            var key = ResourceKey(item);
            if (string.IsNullOrWhiteSpace(key))
            {
                diagnostics.Add(new McpCatalogDiagnostic("invalid_primitive", "resource", string.Empty, provider.Name, "resource has an empty protocol identity."));
                logger.ZLogWarning($"Skip resource with empty identity from provider='{provider.Name}'.");
                continue;
            }

            if (identities.TryAdd(new ResourceIdentity(item)))
            {
                accepted.Add(item);
                continue;
            }

            diagnostics.Add(new McpCatalogDiagnostic("duplicate_primitive", "resource", key, provider.Name,
                $"resource '{key}' was rejected because an earlier provider reserved the protocol identity."));
            logger.ZLogWarning($"Duplicate resource protocol identity '{key}' ignored from provider '{provider.Name}'.");
        }
    }

    private static string ResourceKey(McpRegisteredResource resource) =>
        resource.ProtocolResource?.Uri ?? resource.ProtocolTemplate?.UriTemplate ?? string.Empty;

    private sealed class ResourceIdentity(McpRegisteredResource registration) : McpServerResource
    {
        public override Resource? ProtocolResource => registration.ProtocolResource;
        public override ResourceTemplate ProtocolResourceTemplate => registration.ProtocolTemplate ?? new ResourceTemplate
        {
            UriTemplate = registration.ProtocolResource?.Uri ?? string.Empty,
            Name = registration.ProtocolResource?.Name ?? string.Empty,
            Description = registration.ProtocolResource?.Description
        };
        public override IReadOnlyList<object> Metadata => [];
        public override bool IsMatch(string uri) => false;
        public override ValueTask<ReadResourceResult> ReadAsync(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Resource identities are used only for catalog admission.");
    }
}
