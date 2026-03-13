using System.Diagnostics;
using System.IO;
using ModelContextProtocol.Protocol;
using RevitDevTool.Mcp.Interfaces;
using RevitDevTool.McpParser.Models;
using RevitDevTool.Settings;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.Mcp;

public sealed class ToolRegistryStore(IEnumerable<IMcpRegistryProvider> providers, ISettingsService settingsService)
{
    private readonly IReadOnlyList<IMcpRegistryProvider> _providers = providers.ToList();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, McpRegisteredTool> _byToolId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<McpRegisteredTool>> _byToolName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, McpRegisteredPrompt> _byPromptId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<McpRegisteredPrompt>> _byPromptName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, McpRegisteredResource> _byResourceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<McpRegisteredResource>> _byResourceName = new(StringComparer.OrdinalIgnoreCase);
    public event EventHandler? ToolsChanged;

    public IReadOnlyList<McpRegisteredTool> ToolCatalog { get; private set; } = [];
    public IReadOnlyList<McpRegisteredPrompt> PromptCatalog { get; private set; } = [];
    public IReadOnlyList<McpRegisteredResource> ResourceCatalog { get; private set; } = [];

    public IReadOnlyList<Tool> Tools { get; private set; } = [];
    public IReadOnlyList<Prompt> Prompts { get; private set; } = [];
    public IReadOnlyList<Resource> DirectResources { get; private set; } = [];
    public IReadOnlyList<ResourceTemplate> ResourceTemplates { get; private set; } = [];

    public async Task ReloadAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var loaded = await Task.Run(() =>
            {
                var catalog = LoadFromProviders(
                    settingsService.McpRegistryConfig.DotnetPaths,
                    settingsService.McpRegistryConfig.PythonToolsetPaths);

                McpPathValidator.PruneInvalidConfiguredPaths(settingsService.McpRegistryConfig, catalog);
                return catalog;
            }).ConfigureAwait(false);

            ApplyCatalog(loaded);
        }
        finally
        {
            _gate.Release();
        }

        ToolsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task AddPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var normalizedPath = Path.GetFullPath(path);
        var inputKind = McpPathValidator.ClassifyInputPath(normalizedPath);
        if (inputKind == McpPathValidator.InputKind.Unsupported)
            return;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var dotnetCandidates = settingsService.McpRegistryConfig.DotnetPaths.ToList();
            var pythonCandidates = settingsService.McpRegistryConfig.PythonToolsetPaths.ToList();

            if (inputKind == McpPathValidator.InputKind.DotnetAssembly)
                McpPathValidator.AddDistinct(dotnetCandidates, normalizedPath);
            else if (inputKind == McpPathValidator.InputKind.PythonToolset)
                McpPathValidator.AddDistinct(pythonCandidates, normalizedPath);

            var loaded = await Task.Run(() => LoadFromProviders(dotnetCandidates, pythonCandidates)).ConfigureAwait(false);
            ApplyCatalog(loaded);

            PersistAcceptedPath(inputKind, normalizedPath, loaded);
            McpPathValidator.PruneInvalidConfiguredPaths(settingsService.McpRegistryConfig, loaded);
        }
        finally
        {
            _gate.Release();
        }

        ToolsChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool TryGetTool(string? toolId, string? toolName, out McpRegisteredTool? tool)
    {
        EnsureLoaded();
        return TryGet(toolId, toolName, _byToolId, _byToolName, out tool);
    }

    public bool TryGetPrompt(string? promptId, string? promptName, out McpRegisteredPrompt? prompt)
    {
        EnsureLoaded();
        return TryGet(promptId, promptName, _byPromptId, _byPromptName, out prompt);
    }

    public bool TryGetResource(string? resourceId, string? resourceName, out McpRegisteredResource? resource)
    {
        EnsureLoaded();
        return TryGet(resourceId, resourceName, _byResourceId, _byResourceName, out resource);
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
                return ToolCatalog;

            var catalog = LoadFromProviders(
                settingsService.McpRegistryConfig.DotnetPaths,
                settingsService.McpRegistryConfig.PythonToolsetPaths);

            ApplyCatalog(catalog);
            McpPathValidator.PruneInvalidConfiguredPaths(settingsService.McpRegistryConfig, catalog);
            return ToolCatalog;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool HasLoadedCatalog()
    {
        return (_byToolId.Count > 0 || _byPromptId.Count > 0 || _byResourceId.Count > 0)
               && ToolCatalog.Count == _byToolId.Count
               && PromptCatalog.Count == _byPromptId.Count
               && ResourceCatalog.Count == _byResourceId.Count;
    }

    private McpRegistryCatalog LoadFromProviders(
        IEnumerable<string> dotnetPaths,
        IEnumerable<string> pythonPaths)
    {
        ConfigureProviderPaths(dotnetPaths, pythonPaths);
        ClearIndexes();

        foreach (var provider in _providers.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var catalog = provider.LoadCatalog();
                Trace.TraceInformation(
                    $"[MCP] Provider '{provider.Name}' returned {catalog.Tools.Count} tool(s), {catalog.Prompts.Count} prompt(s), {catalog.Resources.Count} resource(s).");

                CollectToolsFromProvider(provider.Name, catalog.Tools);
                CollectPromptsFromProvider(provider.Name, catalog.Prompts);
                CollectResourcesFromProvider(provider.Name, catalog.Resources);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[MCP] Provider '{provider.Name}' failed: {ex.Message}");
            }
        }

        var loaded = new McpRegistryCatalog
        {
            Tools = _byToolId.Values
                .OrderBy(t => t.Binding.GroupName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.ProtocolTool.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Prompts = _byPromptId.Values
                .OrderBy(t => t.Binding.GroupName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.ProtocolPrompt.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Resources = _byResourceId.Values
                .OrderBy(t => t.Binding.GroupName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ProtocolResource?.Name ?? r.ProtocolTemplate?.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.ProtocolTemplate?.UriTemplate ?? r.ProtocolResource?.Uri ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };

        Trace.TraceInformation(
            $"[MCP] Tool store loaded {loaded.Tools.Count} tool(s), {loaded.Prompts.Count} prompt(s), {loaded.Resources.Count} resource(s).");
        return loaded;
    }

    private void ConfigureProviderPaths(IEnumerable<string> dotnetPaths, IEnumerable<string> pythonPaths)
    {
        var resolvedDotnet = McpPathValidator.ResolvePaths(dotnetPaths, McpPathValidator.IsValidDotnetAssemblyPath);
        var resolvedPython = McpPathValidator.ResolvePaths(pythonPaths, McpPathValidator.IsValidPythonToolsetPath);

        var pathsByMode = new Dictionary<ExecutionMode, IReadOnlyList<string>>
        {
            [ExecutionMode.Assembly] = resolvedDotnet,
            [ExecutionMode.Python] = resolvedPython
        };

        foreach (var provider in _providers)
        {
            if (pathsByMode.TryGetValue(provider.SourceKind, out var paths))
                provider.ConfigurePaths(paths);
        }
    }

    private void ApplyCatalog(McpRegistryCatalog catalog)
    {
        ToolCatalog = catalog.Tools;
        PromptCatalog = catalog.Prompts;
        ResourceCatalog = catalog.Resources;

        Tools = catalog.Tools.Select(t => t.ProtocolTool).ToList();
        Prompts = catalog.Prompts.Select(p => p.ProtocolPrompt).ToList();
        DirectResources = catalog.Resources.Where(r => r.ProtocolResource is not null).Select(r => r.ProtocolResource!).ToList();
        ResourceTemplates = catalog.Resources.Where(r => r.ProtocolTemplate is not null).Select(r => r.ProtocolTemplate!).ToList();
    }

    private void ClearIndexes()
    {
        _byToolId.Clear();
        _byToolName.Clear();
        _byPromptId.Clear();
        _byPromptName.Clear();
        _byResourceId.Clear();
        _byResourceName.Clear();
    }

    private void CollectToolsFromProvider(string providerName, IReadOnlyList<McpRegisteredTool> candidates)
    {
        CollectFromProvider(candidates,
            t => t.Binding.SourcePath,
            t => t.Binding.ContainerType,
            t => t.Binding.MethodName,
            t => TryRegister(providerName, t, t.Id, t.ProtocolTool.Name, _byToolId, _byToolName, "tool"));
    }

    private void CollectPromptsFromProvider(string providerName, IReadOnlyList<McpRegisteredPrompt> candidates)
    {
        CollectFromProvider(candidates,
            p => p.Binding.SourcePath,
            p => p.Binding.ContainerType,
            p => p.Binding.MethodName,
            p => TryRegister(providerName, p, p.Id, p.ProtocolPrompt.Name, _byPromptId, _byPromptName, "prompt"));
    }

    private void CollectResourcesFromProvider(string providerName, IReadOnlyList<McpRegisteredResource> candidates)
    {
        CollectFromProvider(candidates,
            r => r.Binding.SourcePath,
            r => r.Binding.ContainerType,
            r => r.Binding.MethodName,
            r => TryRegister(providerName, r, r.Id, r.ProtocolResource?.Name ?? r.ProtocolTemplate?.Name ?? string.Empty, _byResourceId, _byResourceName, "resource"));
    }

    private static void CollectFromProvider<T>(
        IReadOnlyList<T> candidates,
        Func<T, string> sourcePathKey,
        Func<T, string> containerTypeKey,
        Func<T, string> methodNameKey,
        Action<T> register)
    {
        foreach (var item in candidates
            .OrderBy(sourcePathKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(containerTypeKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(methodNameKey, StringComparer.OrdinalIgnoreCase))
        {
            register(item);
        }
    }

    private void TryRegister<T>(
        string providerName,
        T item,
        string id,
        string name,
        Dictionary<string, T> byId,
        Dictionary<string, List<T>> byName,
        string kind)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Trace.TraceWarning($"[MCP] Skip {kind} with empty name from provider='{providerName}'.");
            return;
        }

        if (byId.ContainsKey(id))
        {
            Trace.TraceWarning($"[MCP] Duplicate {kind} id '{id}' ignored.");
            return;
        }

        byId[id] = item;
        if (!byName.TryGetValue(name, out var nameList))
        {
            nameList = [];
            byName[name] = nameList;
        }

        nameList.Add(item);
    }

    private void PersistAcceptedPath(McpPathValidator.InputKind kind, string normalizedPath, McpRegistryCatalog loadedCatalog)
    {
        switch (kind)
        {
            case McpPathValidator.InputKind.DotnetAssembly when McpPathValidator.PathProducesCatalogItems(normalizedPath, ExecutionMode.Assembly, loadedCatalog):
                McpPathValidator.AddDistinct(settingsService.McpRegistryConfig.DotnetPaths, normalizedPath);
                break;
            case McpPathValidator.InputKind.PythonToolset when McpPathValidator.PathProducesCatalogItems(normalizedPath, ExecutionMode.Python, loadedCatalog):
                McpPathValidator.AddDistinct(settingsService.McpRegistryConfig.PythonToolsetPaths, normalizedPath);
                break;
        }
    }
}