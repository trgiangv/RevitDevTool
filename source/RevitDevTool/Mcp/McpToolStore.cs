using System.Diagnostics;
using System.IO;
using RevitDevTool.Contracts;
using RevitDevTool.Mcp.Interfaces;
using RevitDevTool.Settings;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.Mcp;

public sealed class McpToolStore(IEnumerable<IMcpToolRegistryProvider> providers, ISettingsService settingsService)
{
    private const string PythonToolPattern = "*mcp.py";
    private readonly IReadOnlyList<IMcpToolRegistryProvider> _providers = providers.ToList();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, McpToolDefinition> _byToolId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<McpToolDefinition>> _byName = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? ToolsChanged;
    public IReadOnlyList<McpToolDefinition> Tools { get; private set; } = [];

    public async Task ReloadAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Tools = await Task.Run(() =>
            {
                var loaded = LoadFromProviders(
                    settingsService.McpRegistryConfig.DotnetPaths,
                    settingsService.McpRegistryConfig.PythonToolsetPaths);

                PruneInvalidConfiguredPaths(loaded);
                return loaded;
            }).ConfigureAwait(false);
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
            Tools = loaded;

            PersistAcceptedPath(inputKind, normalizedPath, loaded);
            PruneInvalidConfiguredPaths(loaded);
        }
        finally
        {
            _gate.Release();
        }

        ToolsChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool TryGetTool(string? toolId, string? toolName, out McpToolDefinition? definition)
    {
        EnsureLoaded();

        if (!string.IsNullOrWhiteSpace(toolId) && _byToolId.TryGetValue(toolId!, out var byId))
        {
            definition = byId;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(toolName) && _byName.TryGetValue(toolName!, out var byName) && byName.Count > 0)
        {
            definition = byName[0];
            return true;
        }

        definition = null;
        return false;
    }

    public IReadOnlyList<McpToolDefinition> EnsureLoaded()
    {
        _gate.Wait();
        try
        {
            if (_byToolId.Count > 0 && Tools.Count == _byToolId.Count)
                return Tools;

            Tools = LoadFromProviders(
                settingsService.McpRegistryConfig.DotnetPaths,
                settingsService.McpRegistryConfig.PythonToolsetPaths);

            PruneInvalidConfiguredPaths(Tools);
            return Tools;
        }
        finally
        {
            _gate.Release();
        }
    }

    private IReadOnlyList<McpToolDefinition> LoadFromProviders(
        IEnumerable<string> dotnetPaths,
        IEnumerable<string> pythonPaths)
    {
        ConfigureProviderPaths(dotnetPaths, pythonPaths);

        _byToolId.Clear();
        _byName.Clear();

        foreach (var provider in _providers.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var candidates = provider.LoadTools();
                Trace.TraceInformation($"[MCP] Provider '{provider.Name}' returned {candidates.Count} tool candidate(s).");
                CollectToolsFromProvider(provider.Name, candidates);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[MCP] Provider '{provider.Name}' failed: {ex.Message}");
            }
        }

        var result = _byToolId.Values
            .OrderBy(t => t.GroupName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Trace.TraceInformation($"[MCP] Tool store loaded {result.Count} tool(s).");
        return result;
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

    private void CollectToolsFromProvider(string providerName, IReadOnlyList<McpToolDefinition> candidates)
    {
        foreach (var def in candidates
                     .OrderBy(d => d.SourcePath, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(d => d.ContainerType, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(d => d.MethodName, StringComparer.OrdinalIgnoreCase))
        {
            TryRegisterTool(providerName, def);
        }
    }

    private void TryRegisterTool(string providerName, McpToolDefinition def)
    {
        def.EnsureIdentity();

        if (string.IsNullOrWhiteSpace(def.Name))
        {
            Trace.TraceWarning($"[MCP] Skip tool with empty name from provider='{providerName}'.");
            return;
        }

        if (_byToolId.ContainsKey(def.ToolId))
        {
            Trace.TraceWarning($"[MCP] Duplicate tool id '{def.ToolId}' ignored.");
            return;
        }

        _byToolId[def.ToolId] = def;

        if (!_byName.TryGetValue(def.Name, out var nameList))
        {
            nameList = [];
            _byName[def.Name] = nameList;
        }

        nameList.Add(def);
    }

    private static List<string> ResolvePaths(IEnumerable<string> paths, Func<string?, bool> validator) =>
        paths
            .Where(validator)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private bool PruneInvalidConfiguredPaths(IReadOnlyList<McpToolDefinition> loadedTools)
    {
        var changed = false;
        changed |= RemoveInvalidPaths(settingsService.McpRegistryConfig.DotnetPaths, ExecutionMode.Assembly, loadedTools);
        changed |= RemoveInvalidPaths(settingsService.McpRegistryConfig.PythonToolsetPaths, ExecutionMode.Python, loadedTools);
        return changed;
    }

    private void PersistAcceptedPath(InputKind kind, string normalizedPath, IReadOnlyList<McpToolDefinition> loadedTools)
    {
        switch (kind)
        {
            case InputKind.DotnetAssembly when PathProducesTools(normalizedPath, ExecutionMode.Assembly, loadedTools):
                AddDistinct(settingsService.McpRegistryConfig.DotnetPaths, normalizedPath);
                break;
            case InputKind.PythonToolset when PathProducesTools(normalizedPath, ExecutionMode.Python, loadedTools):
                AddDistinct(settingsService.McpRegistryConfig.PythonToolsetPaths, normalizedPath);
                break;
        }
    }

    private enum InputKind { Unsupported, DotnetAssembly, PythonToolset }

    private static InputKind ClassifyInputPath(string path)
    {
        if (IsValidDotnetAssemblyPath(path)) return InputKind.DotnetAssembly;
        if (IsValidPythonToolsetPath(path)) return InputKind.PythonToolset;
        return InputKind.Unsupported;
    }

    private static bool IsValidDotnetAssemblyPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        File.Exists(path) &&
        string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidPythonToolsetPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        Directory.Exists(path) &&
        Directory.EnumerateFiles(path!, PythonToolPattern, SearchOption.AllDirectories).Any();

    private static bool PathProducesTools(string path, ExecutionMode mode, IReadOnlyList<McpToolDefinition> tools)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = NormalizePath(path);
        return tools.Any(t => t.SourceKind == mode && IsToolFromPath(normalized, t.SourcePath));
    }

    private static bool IsToolFromPath(string configuredPath, string? toolSourcePath)
    {
        if (string.IsNullOrWhiteSpace(toolSourcePath)) return false;
        var normalizedTool = NormalizePath(toolSourcePath!);
        if (string.Equals(configuredPath, normalizedTool, StringComparison.OrdinalIgnoreCase)) return true;
        if (!Directory.Exists(configuredPath)) return false;
        var withSep = configuredPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedTool.StartsWith(withSep, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RemoveInvalidPaths(List<string> paths, ExecutionMode mode, IReadOnlyList<McpToolDefinition> tools)
    {
        var removed = false;
        for (var i = paths.Count - 1; i >= 0; i--)
        {
            if (PathProducesTools(paths[i], mode, tools)) continue;
            Trace.TraceInformation($"[MCP] Remove saved {mode} path '{paths[i]}' because it loaded no tools.");
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
