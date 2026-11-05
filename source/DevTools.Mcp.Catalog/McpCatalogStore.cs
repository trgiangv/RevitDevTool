using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Catalog;
using DevTools.Mcp.Core.Models;
using DevTools.Settings;
using ModelContextProtocol.Protocol;

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
    public IReadOnlyDictionary<string, string> LoadFailures { get; private set; } = RegistryCatalog.Empty.Failures;
    public IReadOnlyList<RegisteredResource> ResourceCatalog { get; private set; } = [];

    public List<Tool> GetToolDescriptors() =>
        RegisteredTools.Select(tool => tool.Descriptor).ToList();

    public List<Resource> GetResourceDescriptors() =>
        ResourceCatalog.Where(r => r.Descriptor is not null).Select(r => r.Descriptor!).ToList();

    public List<ResourceTemplate> GetResourceTemplateDescriptors() =>
        ResourceCatalog.Where(r => r.TemplateDescriptor is not null).Select(r => r.TemplateDescriptor!).ToList();

    public IReadOnlyList<string> ToolsetPaths =>
        [.. settingsService.McpRegistryConfig.DotnetPaths, .. settingsService.McpRegistryConfig.PythonPaths];

    public async Task ReloadAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var config = settingsService.McpRegistryConfig;
            var saved = ToolsetPaths;
            var loaded = await Task.Run(() => catalogLoader.LoadCatalog(config.DotnetPaths, config.PythonPaths)).ConfigureAwait(false);

            McpPathValidator.PruneInvalidConfiguredPaths(config, loaded);
            ApplyLoadedCatalog(loaded);
            SaveIfPathsChanged(saved);
        }
        finally
        {
            _gate.Release();
        }

        CatalogChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<ToolsetChange> AddPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ToolsetChange.Unsupported;

        var normalizedPath = Path.GetFullPath(path);
        var inputKind = McpPathValidator.ClassifyInputPath(normalizedPath);
        if (inputKind == ExecutionMode.Unsupported)
            return ToolsetChange.Unsupported;

        ToolsetChange change;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var config = settingsService.McpRegistryConfig;
            var saved = ToolsetPaths;
            var registered = saved.Any(p => McpPathValidator.IsSamePath(p, normalizedPath));
            var dotnetCandidates = config.DotnetPaths.ToList();
            var pythonCandidates = config.PythonPaths.ToList();

            if (inputKind == ExecutionMode.Dotnet)
                McpPathValidator.AddDistinct(dotnetCandidates, normalizedPath);
            else
                McpPathValidator.AddDistinct(pythonCandidates, normalizedPath);

            var loaded = await Task.Run(() => catalogLoader.LoadCatalog(dotnetCandidates, pythonCandidates)).ConfigureAwait(false);
            ApplyLoadedCatalog(loaded);

            PersistAcceptedPath(inputKind, normalizedPath, loaded);
            McpPathValidator.PruneInvalidConfiguredPaths(config, loaded);
            SaveIfPathsChanged(saved);

            change = !McpPathValidator.PathProducesCatalogItems(normalizedPath, inputKind, loaded)
                ? ToolsetChange.NoItems
                : registered ? ToolsetChange.Reloaded : ToolsetChange.Added;
        }
        finally
        {
            _gate.Release();
        }

        CatalogChanged?.Invoke(this, EventArgs.Empty);
        return change;
    }

    public async Task<ToolsetChange> RemovePathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ToolsetChange.NotRegistered;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var config = settingsService.McpRegistryConfig;
            var removed = config.DotnetPaths.RemoveAll(p => McpPathValidator.IsSamePath(p, path))
                          + config.PythonPaths.RemoveAll(p => McpPathValidator.IsSamePath(p, path));
            if (removed == 0)
                return ToolsetChange.NotRegistered;

            settingsService.SaveSettings();
            var loaded = await Task.Run(() => catalogLoader.LoadCatalog(config.DotnetPaths, config.PythonPaths)).ConfigureAwait(false);
            ApplyLoadedCatalog(loaded);
        }
        finally
        {
            _gate.Release();
        }

        CatalogChanged?.Invoke(this, EventArgs.Empty);
        return ToolsetChange.Removed;
    }

    private void ApplyLoadedCatalog(RegistryCatalog catalog)
    {
        ApplyCatalog(catalog);
        _contentHash = McpCatalogHash.Compute(catalog);
    }

    private void SaveIfPathsChanged(IReadOnlyList<string> saved)
    {
        if (!saved.SequenceEqual(ToolsetPaths, StringComparer.OrdinalIgnoreCase))
            settingsService.SaveSettings();
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
        if (id is not null && !string.IsNullOrWhiteSpace(id) && byId.TryGetValue(id, out var byIdResult))
        {
            result = byIdResult;
            return true;
        }
        if (name is not null && !string.IsNullOrWhiteSpace(name) && byName.TryGetValue(name, out var byNameList) && byNameList.Count > 0)
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
        var hash = McpCatalogHash.Compute(catalog);
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
        LoadFailures = catalog.Failures;
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
