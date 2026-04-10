using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ModelContextProtocol.Protocol;
using RevitDevTool.ExternalExecution.Mcp.Registry;
using RevitDevTool.McpParser.Models;
using RevitDevTool.Settings;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.ExternalExecution.Mcp;

public sealed class ToolRegistryStore(ToolRegistryCatalogLoader catalogLoader, ISettingsService settingsService)
{
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
                var catalog = catalogLoader.LoadCatalog(
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

            var loaded = await Task.Run(() => catalogLoader.LoadCatalog(dotnetCandidates, pythonCandidates)).ConfigureAwait(false);
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

    public bool TryResolveResourceByUri(string uri, out McpRegisteredResource? resource)
    {
        EnsureLoaded();

        resource = default;
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        foreach (var candidate in ResourceCatalog)
        {
            if (UriMatches(candidate, uri))
            {
                resource = candidate;
                return true;
            }
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
                return ToolCatalog;

            var catalog = catalogLoader.LoadCatalog(
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
        if (ToolCatalog.Count == 0 && PromptCatalog.Count == 0 && ResourceCatalog.Count == 0)
            return false;

        return ToolCatalog.Count == _byToolId.Count
               && PromptCatalog.Count == _byPromptId.Count
               && ResourceCatalog.Count == _byResourceId.Count
               && IndexesMatchCatalog();
    }

    private void ApplyCatalog(McpRegistryCatalog catalog)
    {
        ClearIndexes();

        ToolCatalog = catalog.Tools;
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
    }

    private bool IndexesMatchCatalog()
    {
        foreach (var tool in ToolCatalog)
        {
            if (!_byToolId.ContainsKey(tool.Id))
                return false;
        }

        foreach (var prompt in PromptCatalog)
        {
            if (!_byPromptId.ContainsKey(prompt.Id))
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

    private static bool UriMatches(McpRegisteredResource candidate, string uri)
    {
        var directUri = candidate.ProtocolResource?.Uri;
        if (!string.IsNullOrWhiteSpace(directUri)
            && string.Equals(directUri, uri, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (candidate.ProtocolTemplate?.UriTemplate is not string uriTemplate || string.IsNullOrWhiteSpace(uriTemplate))
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
