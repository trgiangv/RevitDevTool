using System.Text;
using System.Text.RegularExpressions;
using DevTools.Settings;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Mcp;

public sealed class McpCatalogStore(
    McpCatalogLoader catalogLoader,
    ISettingsService settingsService,
    McpServerPrimitiveCollection<McpServerTool> serverTools,
    McpServerPrimitiveCollection<McpServerPrompt> serverPrompts,
    McpServerResourceCollection serverResources)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, McpRegisteredTool> _byToolId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<McpRegisteredTool>> _byToolName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, McpRegisteredPrompt> _byPromptId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<McpRegisteredPrompt>> _byPromptName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, McpRegisteredResource> _byResourceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<McpRegisteredResource>> _byResourceName = new(StringComparer.OrdinalIgnoreCase);
    private McpPrimitiveSnapshot _snapshot = McpPrimitiveSnapshot.Empty;
    private long _generation;
    public event EventHandler? CatalogChanged;

    public IReadOnlyList<McpRegisteredTool> RegisteredTools { get; private set; } = [];
    public IReadOnlyList<McpRegisteredPrompt> PromptCatalog { get; private set; } = [];
    public IReadOnlyList<McpRegisteredResource> ResourceCatalog { get; private set; } = [];

    public IReadOnlyList<Tool> Tools { get; private set; } = [];
    public IReadOnlyList<Prompt> Prompts { get; private set; } = [];
    public IReadOnlyList<Resource> DirectResources { get; private set; } = [];
    public IReadOnlyList<ResourceTemplate> ResourceTemplates { get; private set; } = [];
    public IReadOnlyList<McpCatalogDiagnostic> Diagnostics => _snapshot.Diagnostics;
    public bool IsLoaded => Volatile.Read(ref _generation) > 0;
    public long Generation => Volatile.Read(ref _generation);

    public async Task ReloadAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var loaded = await Task.Run(() =>
            {
                var loaded = catalogLoader.LoadCatalog(
                    settingsService.McpRegistryConfig.DotnetPaths,
                    settingsService.McpRegistryConfig.PythonToolsetPaths);

                McpPathValidator.PruneInvalidConfiguredPaths(settingsService.McpRegistryConfig, loaded.Catalog);
                return loaded;
            }).ConfigureAwait(false);

            ApplyCatalog(loaded);
        }
        finally
        {
            _gate.Release();
        }

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
            ApplyCatalog(loaded);

            PersistAcceptedPath(inputKind, normalizedPath, loaded.Catalog);
            McpPathValidator.PruneInvalidConfiguredPaths(settingsService.McpRegistryConfig, loaded.Catalog);
        }
        finally
        {
            _gate.Release();
        }

        CatalogChanged?.Invoke(this, EventArgs.Empty);
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
            if (IsLoaded)
                return RegisteredTools;

            var loaded = catalogLoader.LoadCatalog(
                settingsService.McpRegistryConfig.DotnetPaths,
                settingsService.McpRegistryConfig.PythonToolsetPaths);

            ApplyCatalog(loaded);
            McpPathValidator.PruneInvalidConfiguredPaths(settingsService.McpRegistryConfig, loaded.Catalog);
            return RegisteredTools;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ApplyCatalog(McpCatalogLoadResult loaded)
    {
        var catalog = loaded.Catalog;
        ClearIndexes();

        RegisteredTools = catalog.Tools;
        PromptCatalog = catalog.Prompts;
        ResourceCatalog = catalog.Resources;

        IndexCatalogItems(catalog.Tools, _byToolId, _byToolName, tool => tool.Id, tool => tool.ProtocolTool.Name);
        IndexCatalogItems(catalog.Prompts, _byPromptId, _byPromptName, prompt => prompt.Id, prompt => prompt.ProtocolPrompt.Name);
        IndexCatalogItems(catalog.Resources, _byResourceId, _byResourceName,
            resource => resource.Id,
            resource => resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name ?? string.Empty);

        Tools = catalog.Tools.Select(t => t.ProtocolTool).ToList();
        Prompts = catalog.Prompts.Select(p => p.ProtocolPrompt).ToList();
        DirectResources = catalog.Resources.Where(r => r.ProtocolResource is not null).Select(r => r.ProtocolResource!).ToList();
        ResourceTemplates = catalog.Resources.Where(r => r.ProtocolTemplate is not null).Select(r => r.ProtocolTemplate!).ToList();
        ApplySnapshot(loaded.Snapshot);
    }

    private void ApplySnapshot(McpPrimitiveSnapshot snapshot)
    {
        serverTools.Clear();
        foreach (var tool in snapshot.Tools)
            serverTools.TryAdd(tool);

        serverPrompts.Clear();
        foreach (var prompt in snapshot.Prompts)
            serverPrompts.TryAdd(prompt);

        serverResources.Clear();
        foreach (var resource in snapshot.Resources)
            serverResources.TryAdd(resource);

        _snapshot = snapshot;
        Interlocked.Increment(ref _generation);
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
        var directUri = candidate.ProtocolResource?.Uri;
        if (!string.IsNullOrWhiteSpace(directUri)
            && string.Equals(directUri, uri, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (candidate.ProtocolTemplate?.UriTemplate is not { } uriTemplate || string.IsNullOrWhiteSpace(uriTemplate))
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
