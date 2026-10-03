using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Catalog;
using DevTools.Mcp.Core.Models;
using DevTools.Settings;
using ModelContextProtocol.Protocol;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Mcp;

public sealed class McpCatalogStore(ICatalogLoader catalogLoader, ISettingsService settingsService)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, RegisteredTool> _byToolId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<RegisteredTool>> _byToolName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RegisteredResource> _byResourceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<RegisteredResource>> _byResourceName = new(StringComparer.OrdinalIgnoreCase);
    private string? _contentHash;
    public event EventHandler? CatalogChanged;

    public IReadOnlyList<RegisteredTool> RegisteredTools { get; private set; } = [];
    public IReadOnlyList<RegisteredResource> ResourceCatalog { get; private set; } = [];

    public List<Tool> GetToolDescriptors() =>
        RegisteredTools.Select(tool => tool.Descriptor).ToList();

    public List<Resource> GetResourceDescriptors() =>
        ResourceCatalog.Where(r => r.Descriptor is not null).Select(r => r.Descriptor!).ToList();

    public List<ResourceTemplate> GetResourceTemplateDescriptors() =>
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
                    settingsService.McpRegistryConfig.PythonPaths);

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

        bool changed;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var dotnetCandidates = settingsService.McpRegistryConfig.DotnetPaths.ToList();
            var pythonCandidates = settingsService.McpRegistryConfig.PythonPaths.ToList();

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

    public bool TryGetTool(string? toolId, string? toolName, out RegisteredTool? tool)
    {
        EnsureLoaded();
        return TryGet(toolId, toolName, _byToolId, _byToolName, out tool);
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

    public IReadOnlyList<RegisteredTool> EnsureLoaded()
    {
        _gate.Wait();
        try
        {
            if (HasLoadedCatalog())
                return RegisteredTools;

            var catalog = catalogLoader.LoadCatalog(
                settingsService.McpRegistryConfig.DotnetPaths,
                settingsService.McpRegistryConfig.PythonPaths);

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

    private bool TryApplyCatalog(RegistryCatalog catalog)
    {
        var hash = McpCatalogContentHash.Compute(catalog);
        if (CatalogIdsMatch(catalog) && string.Equals(_contentHash, hash, StringComparison.Ordinal))
            return false;

        ApplyCatalog(catalog);
        _contentHash = hash;
        return true;
    }

    private bool CatalogIdsMatch(RegistryCatalog catalog)
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

    private void ApplyCatalog(RegistryCatalog catalog)
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

    private void PersistAcceptedPath(ExecutionMode kind, string normalizedPath, RegistryCatalog loadedCatalog)
    {
        switch (kind)
        {
            case ExecutionMode.Dotnet when McpPathValidator.PathProducesCatalogItems(normalizedPath, ExecutionMode.Dotnet, loadedCatalog):
                McpPathValidator.AddDistinct(settingsService.McpRegistryConfig.DotnetPaths, normalizedPath);
                break;
            case ExecutionMode.Python when McpPathValidator.PathProducesCatalogItems(normalizedPath, ExecutionMode.Python, loadedCatalog):
                McpPathValidator.AddDistinct(settingsService.McpRegistryConfig.PythonPaths, normalizedPath);
                break;
        }
    }
}
