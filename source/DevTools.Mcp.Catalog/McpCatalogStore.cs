using System.Text;
using System.Text.RegularExpressions;
using DevTools.Settings;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Mcp.Catalog;

public sealed class McpCatalogStore(IMcpCatalogLoader catalogLoader, ISettingsService settingsService) : IHostPrimitiveRegistry
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, McpRegisteredTool> _byToolId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<McpRegisteredTool>> _byToolName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, McpRegisteredResource> _byResourceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<McpRegisteredResource>> _byResourceName = new(StringComparer.OrdinalIgnoreCase);
    public event EventHandler? CatalogChanged;

    public IReadOnlyList<McpRegisteredTool> RegisteredTools { get; private set; } = [];
    public IReadOnlyList<McpRegisteredResource> ResourceCatalog { get; private set; } = [];

    public IReadOnlyList<McpToolDescriptor> ToolDescriptors =>
        RegisteredTools.Select(tool => tool.Descriptor).ToList();

    public IReadOnlyList<McpResourceDescriptor> ResourceDescriptors =>
        ResourceCatalog.Where(r => r.Descriptor is not null).Select(r => r.Descriptor!).ToList();

    public IReadOnlyList<McpResourceTemplateDescriptor> ResourceTemplateDescriptors =>
        ResourceCatalog.Where(r => r.TemplateDescriptor is not null).Select(r => r.TemplateDescriptor!).ToList();

    public async Task ReloadAsync()
    {
        bool changed;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var loaded = await Task.Run(() =>
            {
                var catalog = catalogLoader.LoadCatalog(
                    settingsService.McpRegistryConfig.DotnetPaths,
                    settingsService.McpRegistryConfig.PythonToolsetPaths);

                McpPathValidator.PruneInvalidConfiguredPaths(settingsService.McpRegistryConfig, catalog);
                return catalog;
            }).ConfigureAwait(false);

            changed = TryApplyCatalog(loaded);
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
            CatalogChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task AddPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var normalizedPath = Path.GetFullPath(path);
        var inputKind = McpPathValidator.ClassifyInputPath(normalizedPath);
        if (inputKind == ExecutionMode.Unsupported)
            return;

        var changed = false;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var dotnetCandidates = settingsService.McpRegistryConfig.DotnetPaths.ToList();
            var pythonCandidates = settingsService.McpRegistryConfig.PythonToolsetPaths.ToList();

            if (inputKind == ExecutionMode.Dotnet)
                McpPathValidator.AddDistinct(dotnetCandidates, normalizedPath);
            else if (inputKind == ExecutionMode.Python)
                McpPathValidator.AddDistinct(pythonCandidates, normalizedPath);

            var loaded = await Task.Run(() => catalogLoader.LoadCatalog(dotnetCandidates, pythonCandidates)).ConfigureAwait(false);
            changed = TryApplyCatalog(loaded);

            PersistAcceptedPath(inputKind, normalizedPath, loaded);
            McpPathValidator.PruneInvalidConfiguredPaths(settingsService.McpRegistryConfig, loaded);
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
            CatalogChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool TryGetTool(string? toolId, string? toolName, out McpRegisteredTool? tool)
    {
        EnsureLoaded();
        return TryGet(toolId, toolName, _byToolId, _byToolName, out tool);
    }

    public bool TryResolveResourceByUri(string uri, out McpRegisteredResource? resource)
    {
        EnsureLoaded();

        resource = null;
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        foreach (var candidate in ResourceCatalog)
        {
            if (!UriMatches(candidate, uri)) continue;
            resource = candidate;
            return true;
        }

        return false;
    }

    private static bool TryGet<T>(
        string? id,
        string? name,
        Dictionary<string, T> byId,
        Dictionary<string, List<T>> byName,
        out T? result)
    {
        if (!string.IsNullOrWhiteSpace(id) && byId.TryGetValue(id!, out var byIdResult))
        {
            result = byIdResult;
            return true;
        }
        if (!string.IsNullOrWhiteSpace(name) && byName.TryGetValue(name!, out var byNameList) && byNameList.Count > 0)
        {
            result = byNameList[0];
            return true;
        }
        result = default;
        return false;
    }

    public IReadOnlyList<McpRegisteredTool> EnsureLoaded()
    {
        _gate.Wait();
        try
        {
            if (HasLoadedCatalog())
                return RegisteredTools;

            var catalog = catalogLoader.LoadCatalog(
                settingsService.McpRegistryConfig.DotnetPaths,
                settingsService.McpRegistryConfig.PythonToolsetPaths);

            TryApplyCatalog(catalog);
            McpPathValidator.PruneInvalidConfiguredPaths(settingsService.McpRegistryConfig, catalog);
            return RegisteredTools;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool HasLoadedCatalog()
    {
        if (RegisteredTools.Count == 0 && ResourceCatalog.Count == 0)
            return false;

        return RegisteredTools.Count == _byToolId.Count
               && ResourceCatalog.Count == _byResourceId.Count
               && IndexesMatchCatalog();
    }

    private bool TryApplyCatalog(McpRegistryCatalog catalog)
    {
        if (CatalogIdsMatch(catalog))
            return false;

        ApplyCatalog(catalog);
        return true;
    }

    private bool CatalogIdsMatch(McpRegistryCatalog catalog)
    {
        if (RegisteredTools.Count != catalog.Tools.Count || ResourceCatalog.Count != catalog.Resources.Count)
            return false;

        foreach (var tool in catalog.Tools)
        {
            if (!_byToolId.ContainsKey(tool.Id))
                return false;
        }

        foreach (var resource in catalog.Resources)
        {
            if (!_byResourceId.ContainsKey(resource.Id))
                return false;
        }

        return true;
    }

    private void ApplyCatalog(McpRegistryCatalog catalog)
    {
        ClearIndexes();

        RegisteredTools = catalog.Tools;
        ResourceCatalog = catalog.Resources;

        IndexCatalogItems(catalog.Tools, _byToolId, _byToolName, tool => tool.Id, tool => tool.Descriptor.Name);
        IndexCatalogItems(catalog.Resources, _byResourceId, _byResourceName,
            resource => resource.Id,
            resource => resource.DisplayName);
    }

    private bool IndexesMatchCatalog()
    {
        foreach (var tool in RegisteredTools)
        {
            if (!_byToolId.ContainsKey(tool.Id))
                return false;
        }

        foreach (var resource in ResourceCatalog)
        {
            if (!_byResourceId.ContainsKey(resource.Id))
                return false;
        }

        return true;
    }

    private void ClearIndexes()
    {
        _byToolId.Clear();
        _byToolName.Clear();
        _byResourceId.Clear();
        _byResourceName.Clear();
    }

    private static void IndexCatalogItems<T>(
        IReadOnlyList<T> items,
        Dictionary<string, T> byId,
        Dictionary<string, List<T>> byName,
        Func<T, string> idSelector,
        Func<T, string> nameSelector)
    {
        foreach (var item in items)
        {
            var id = idSelector(item);
            var name = nameSelector(item);

            byId[id] = item;
            if (!byName.TryGetValue(name, out var nameList))
            {
                nameList = [];
                byName[name] = nameList;
            }

            nameList.Add(item);
        }
    }

    private void PersistAcceptedPath(ExecutionMode kind, string normalizedPath, McpRegistryCatalog loadedCatalog)
    {
        switch (kind)
        {
            case ExecutionMode.Dotnet when McpPathValidator.PathProducesCatalogItems(normalizedPath, ExecutionMode.Dotnet, loadedCatalog):
                McpPathValidator.AddDistinct(settingsService.McpRegistryConfig.DotnetPaths, normalizedPath);
                break;
            case ExecutionMode.Python when McpPathValidator.PathProducesCatalogItems(normalizedPath, ExecutionMode.Python, loadedCatalog):
                McpPathValidator.AddDistinct(settingsService.McpRegistryConfig.PythonToolsetPaths, normalizedPath);
                break;
        }
    }

    private static bool UriMatches(McpRegisteredResource candidate, string uri)
    {
        var directUri = candidate.Descriptor?.Uri;
        if (!string.IsNullOrWhiteSpace(directUri)
            && string.Equals(directUri, uri, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (candidate.TemplateDescriptor?.UriTemplate is not { } uriTemplate || string.IsNullOrWhiteSpace(uriTemplate))
            return false;

        return TemplateMatches(uriTemplate, uri);
    }

    private static bool TemplateMatches(string uriTemplate, string uri)
    {
        var pattern = BuildTemplatePattern(uriTemplate);
        return Regex.IsMatch(uri, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string BuildTemplatePattern(string uriTemplate)
    {
        var pattern = new StringBuilder("^");
        var index = 0;

        while (index < uriTemplate.Length)
        {
            var openBrace = uriTemplate.IndexOf('{', index);
            if (openBrace < 0)
            {
                pattern.Append(Regex.Escape(uriTemplate[index..]));
                break;
            }

            pattern.Append(Regex.Escape(uriTemplate[index..openBrace]));

            var closeBrace = uriTemplate.IndexOf('}', openBrace + 1);
            if (closeBrace < 0)
            {
                pattern.Append(Regex.Escape(uriTemplate[openBrace..]));
                break;
            }

            pattern.Append("[^/]+?");
            index = closeBrace + 1;
        }

        pattern.Append('$');
        return pattern.ToString();
    }
}
