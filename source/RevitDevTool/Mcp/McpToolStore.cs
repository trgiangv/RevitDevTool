using System.Diagnostics;
using System.IO;
using ModelContextProtocol.Protocol;
using RevitDevTool.Contracts;
using RevitDevTool.Mcp.Interfaces;
using RevitDevTool.Mcp.Parser.Models;
using RevitDevTool.Settings;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.Mcp;

public sealed class McpToolStore(IEnumerable<IMcpRegistryProvider> providers, ISettingsService settingsService)
{
    private const string PythonToolPattern = "*mcp.py";
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

                PruneInvalidConfiguredPaths(catalog);
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
        var inputKind = ClassifyInputPath(normalizedPath);
        if (inputKind == InputKind.Unsupported)
            return;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var dotnetCandidates = settingsService.McpRegistryConfig.DotnetPaths.ToList();
            var pythonCandidates = settingsService.McpRegistryConfig.PythonToolsetPaths.ToList();

            if (inputKind == InputKind.DotnetAssembly)
                AddDistinct(dotnetCandidates, normalizedPath);
            else if (inputKind == InputKind.PythonToolset)
                AddDistinct(pythonCandidates, normalizedPath);

            var loaded = await Task.Run(() => LoadFromProviders(dotnetCandidates, pythonCandidates)).ConfigureAwait(false);
            ApplyCatalog(loaded);

            PersistAcceptedPath(inputKind, normalizedPath, loaded);
            PruneInvalidConfiguredPaths(loaded);
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

        if (!string.IsNullOrWhiteSpace(toolId) && _byToolId.TryGetValue(toolId!, out var byId))
        {
            tool = byId;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(toolName) && _byToolName.TryGetValue(toolName!, out var byName) && byName.Count > 0)
        {
            tool = byName[0];
            return true;
        }

        tool = null;
        return false;
    }

    public bool TryGetPrompt(string? promptId, string? promptName, out McpRegisteredPrompt? prompt)
    {
        EnsureLoaded();

        if (!string.IsNullOrWhiteSpace(promptId) && _byPromptId.TryGetValue(promptId!, out var byId))
        {
            prompt = byId;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(promptName) && _byPromptName.TryGetValue(promptName!, out var byName) && byName.Count > 0)
        {
            prompt = byName[0];
            return true;
        }

        prompt = null;
        return false;
    }

    public bool TryGetResource(string? resourceId, string? resourceName, out McpRegisteredResource? resource)
    {
        EnsureLoaded();

        if (!string.IsNullOrWhiteSpace(resourceId) && _byResourceId.TryGetValue(resourceId!, out var byId))
        {
            resource = byId;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(resourceName) && _byResourceName.TryGetValue(resourceName!, out var byName) && byName.Count > 0)
        {
            resource = byName[0];
            return true;
        }

        resource = null;
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
            PruneInvalidConfiguredPaths(catalog);
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
        var resolvedDotnet = ResolvePaths(dotnetPaths, IsValidDotnetAssemblyPath);
        var resolvedPython = ResolvePaths(pythonPaths, IsValidPythonToolsetPath);

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
        foreach (var tool in candidates
                     .OrderBy(d => d.Binding.SourcePath, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(d => d.Binding.ContainerType, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(d => d.Binding.MethodName, StringComparer.OrdinalIgnoreCase))
        {
            TryRegisterTool(providerName, tool);
        }
    }

    private void CollectPromptsFromProvider(string providerName, IReadOnlyList<McpRegisteredPrompt> candidates)
    {
        foreach (var prompt in candidates
                     .OrderBy(d => d.Binding.SourcePath, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(d => d.Binding.ContainerType, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(d => d.Binding.MethodName, StringComparer.OrdinalIgnoreCase))
        {
            TryRegisterPrompt(providerName, prompt);
        }
    }

    private void CollectResourcesFromProvider(string providerName, IReadOnlyList<McpRegisteredResource> candidates)
    {
        foreach (var resource in candidates
                     .OrderBy(d => d.Binding.SourcePath, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(d => d.Binding.ContainerType, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(d => d.Binding.MethodName, StringComparer.OrdinalIgnoreCase))
        {
            TryRegisterResource(providerName, resource);
        }
    }

    private void TryRegisterTool(string providerName, McpRegisteredTool tool)
    {
        if (string.IsNullOrWhiteSpace(tool.ProtocolTool.Name))
        {
            Trace.TraceWarning($"[MCP] Skip tool with empty name from provider='{providerName}'.");
            return;
        }

        if (_byToolId.ContainsKey(tool.Id))
        {
            Trace.TraceWarning($"[MCP] Duplicate tool id '{tool.Id}' ignored.");
            return;
        }

        _byToolId[tool.Id] = tool;

        if (!_byToolName.TryGetValue(tool.ProtocolTool.Name, out var nameList))
        {
            nameList = [];
            _byToolName[tool.ProtocolTool.Name] = nameList;
        }

        nameList.Add(tool);
    }

    private void TryRegisterPrompt(string providerName, McpRegisteredPrompt prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt.ProtocolPrompt.Name))
        {
            Trace.TraceWarning($"[MCP] Skip prompt with empty name from provider='{providerName}'.");
            return;
        }

        if (_byPromptId.ContainsKey(prompt.Id))
        {
            Trace.TraceWarning($"[MCP] Duplicate prompt id '{prompt.Id}' ignored.");
            return;
        }

        _byPromptId[prompt.Id] = prompt;

        if (!_byPromptName.TryGetValue(prompt.ProtocolPrompt.Name, out var nameList))
        {
            nameList = [];
            _byPromptName[prompt.ProtocolPrompt.Name] = nameList;
        }

        nameList.Add(prompt);
    }

    private void TryRegisterResource(string providerName, McpRegisteredResource resource)
    {
        var name = resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            Trace.TraceWarning($"[MCP] Skip resource with empty name from provider='{providerName}'.");
            return;
        }

        if (_byResourceId.ContainsKey(resource.Id))
        {
            Trace.TraceWarning($"[MCP] Duplicate resource id '{resource.Id}' ignored.");
            return;
        }

        _byResourceId[resource.Id] = resource;

        if (!_byResourceName.TryGetValue(name, out var nameList))
        {
            nameList = [];
            _byResourceName[name] = nameList;
        }

        nameList.Add(resource);
    }

    private static List<string> ResolvePaths(IEnumerable<string> paths, Func<string?, bool> validator) =>
        paths
            .Where(validator)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private bool PruneInvalidConfiguredPaths(McpRegistryCatalog loadedCatalog)
    {
        var changed = false;
        changed |= RemoveInvalidPaths(settingsService.McpRegistryConfig.DotnetPaths, ExecutionMode.Assembly, loadedCatalog);
        changed |= RemoveInvalidPaths(settingsService.McpRegistryConfig.PythonToolsetPaths, ExecutionMode.Python, loadedCatalog);
        return changed;
    }

    private void PersistAcceptedPath(InputKind kind, string normalizedPath, McpRegistryCatalog loadedCatalog)
    {
        switch (kind)
        {
            case InputKind.DotnetAssembly when PathProducesCatalogItems(normalizedPath, ExecutionMode.Assembly, loadedCatalog):
                AddDistinct(settingsService.McpRegistryConfig.DotnetPaths, normalizedPath);
                break;
            case InputKind.PythonToolset when PathProducesCatalogItems(normalizedPath, ExecutionMode.Python, loadedCatalog):
                AddDistinct(settingsService.McpRegistryConfig.PythonToolsetPaths, normalizedPath);
                break;
        }
    }

    private enum InputKind
    {
        Unsupported,
        DotnetAssembly,
        PythonToolset
    }

    private static InputKind ClassifyInputPath(string path)
    {
        if (IsValidDotnetAssemblyPath(path))
            return InputKind.DotnetAssembly;
        if (IsValidPythonToolsetPath(path))
            return InputKind.PythonToolset;
        return InputKind.Unsupported;
    }

    private static bool IsValidDotnetAssemblyPath(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && File.Exists(path)
        && string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidPythonToolsetPath(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && Directory.Exists(path)
        && Directory.EnumerateFiles(path!, PythonToolPattern, SearchOption.AllDirectories).Any();

    private static bool PathProducesCatalogItems(string path, ExecutionMode mode, McpRegistryCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalized = NormalizePath(path);
        return catalog.Tools.Any(t => t.Binding.SourceKind == mode && IsItemFromPath(normalized, t.Binding.SourcePath))
            || catalog.Prompts.Any(t => t.Binding.SourceKind == mode && IsItemFromPath(normalized, t.Binding.SourcePath))
            || catalog.Resources.Any(t => t.Binding.SourceKind == mode && IsItemFromPath(normalized, t.Binding.SourcePath));
    }

    private static bool IsItemFromPath(string configuredPath, string? itemSourcePath)
    {
        if (string.IsNullOrWhiteSpace(itemSourcePath))
            return false;

        var normalizedItem = NormalizePath(itemSourcePath!);
        if (string.Equals(configuredPath, normalizedItem, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!Directory.Exists(configuredPath))
            return false;

        var withSep = configuredPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedItem.StartsWith(withSep, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RemoveInvalidPaths(List<string> paths, ExecutionMode mode, McpRegistryCatalog catalog)
    {
        var removed = false;
        for (var i = paths.Count - 1; i >= 0; i--)
        {
            if (PathProducesCatalogItems(paths[i], mode, catalog))
                continue;

            Trace.TraceInformation($"[MCP] Remove saved {mode} path '{paths[i]}' because it loaded no primitives.");
            paths.RemoveAt(i);
            removed = true;
        }

        return removed;
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static void AddDistinct(List<string> paths, string path)
    {
        if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase))
            paths.Add(path);
    }
}