using System.IO;
using System.Diagnostics;
using RevitDevTool.Execution.Models;
using RevitDevTool.Mcp.Interfaces;
using RevitDevTool.Mcp.Schemas;
using RevitDevTool.Settings;
namespace RevitDevTool.Mcp;

public sealed class McpRegistryService(McpToolRegistry toolRegistry, IEnumerable<IMcpToolRegistryProvider> providers, ISettingsService settingsService)
{
    private const string PythonToolPattern = "*mcp.py";
    private readonly IReadOnlyList<IMcpToolRegistryProvider> _providers = providers.ToList();
    private readonly SemaphoreSlim _reloadGate = new(1, 1);

    private enum RegistryInputKind
    {
        Unsupported,
        DotnetAssembly,
        PythonToolset
    }

    public event EventHandler? RegistryChanged;

    public IReadOnlyList<McpToolDefinition> Tools { get; private set; } = [];

    public async Task InitializeAsync()
    {
        await ReloadAsync().ConfigureAwait(true);
    }

    public IReadOnlyList<McpToolDefinition> EnsureToolsLoaded()
    {
        _reloadGate.Wait();
        try
        {
            var liveTools = toolRegistry.GetTools();
            if (liveTools.Count == 0 || Tools.Count != liveTools.Count)
            {
                liveTools = ReloadTools(
                    settingsService.McpRegistryConfig.DotnetPaths,
                    settingsService.McpRegistryConfig.PythonToolsetPaths);

                PruneInvalidConfiguredPaths(liveTools);
                Trace.TraceInformation($"[MCP] EnsureToolsLoaded refreshed registry. live={liveTools.Count}, cached={Tools.Count}");
            }

            Tools = liveTools;
            return Tools;
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    public bool TryGetTool(string? toolId, string? toolName, out McpToolDefinition? definition)
    {
        EnsureToolsLoaded();
        if (!string.IsNullOrWhiteSpace(toolId) && toolRegistry.TryGetByToolId(toolId!, out definition))
            return true;

        if (!string.IsNullOrWhiteSpace(toolName) && toolRegistry.TryGetByName(toolName!, out definition))
            return true;

        definition = null;
        return false;
    }



    public async Task ReloadAsync()
    {
        await _reloadGate.WaitAsync().ConfigureAwait(true);
        try
        {
            Tools = await Task.Run(() =>
            {
                var loadedTools = ReloadTools(
                    settingsService.McpRegistryConfig.DotnetPaths,
                    settingsService.McpRegistryConfig.PythonToolsetPaths);

                PruneInvalidConfiguredPaths(loadedTools);

                return loadedTools;
            }).ConfigureAwait(true);
        }
        finally
        {
            _reloadGate.Release();
        }

        RegistryChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task AddPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var normalizedPath = Path.GetFullPath(path);
        var inputKind = ClassifyInputPath(normalizedPath);
        if (inputKind == RegistryInputKind.Unsupported)
            return;

        var dotnetCandidatePaths = BuildDotnetCandidates(inputKind, normalizedPath);
        var pythonCandidatePaths = BuildPythonCandidates(inputKind, normalizedPath);

        await _reloadGate.WaitAsync().ConfigureAwait(true);
        try
        {
            var loadedTools = await LoadCandidateToolsAsync(dotnetCandidatePaths, pythonCandidatePaths).ConfigureAwait(true);
            Tools = loadedTools;
            PersistAcceptedPath(inputKind, normalizedPath, loadedTools);
            PruneInvalidConfiguredPaths(loadedTools);

        }
        finally
        {
            _reloadGate.Release();
        }

        RegistryChanged?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<McpToolDefinition> ReloadTools(
        IEnumerable<string> dotnetPaths,
        IEnumerable<string> pythonPaths)
    {
        var resolvedDotnetDlls = dotnetPaths
            .Where(IsValidDotnetAssemblyPath)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normalizedPythonPaths = pythonPaths
            .Where(IsValidPythonToolsetPath)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ConfigureProviders(resolvedDotnetDlls, normalizedPythonPaths);

        toolRegistry.Reload();
        return toolRegistry.GetTools();
    }

    private void ConfigureProviders(
        IReadOnlyList<string> normalizedDotnetPaths,
        IReadOnlyList<string> normalizedPythonPaths)
    {
        var pathsByMode = new Dictionary<ExecutionMode, IReadOnlyList<string>>
        {
            [ExecutionMode.Assembly] = normalizedDotnetPaths,
            [ExecutionMode.Python] = normalizedPythonPaths
        };

        foreach (var provider in _providers)
        {
            if (pathsByMode.TryGetValue(provider.SourceKind, out var paths))
                provider.ConfigurePaths(paths);
        }
    }


    private bool PruneInvalidConfiguredPaths(IReadOnlyList<McpToolDefinition> loadedTools)
    {
        var changed = false;

        changed |= RemoveInvalidPaths(
            settingsService.McpRegistryConfig.DotnetPaths,
            ExecutionMode.Assembly,
            loadedTools);

        changed |= RemoveInvalidPaths(
            settingsService.McpRegistryConfig.PythonToolsetPaths,
            ExecutionMode.Python,
            loadedTools);

        return changed;
    }

    private static bool RemoveInvalidPaths(
        List<string> configuredPaths,
        ExecutionMode executionMode,
        IReadOnlyList<McpToolDefinition> loadedTools)
    {
        var removedAny = false;
        for (var index = configuredPaths.Count - 1; index >= 0; index--)
        {
            var path = configuredPaths[index];
            if (PathProducesTools(path, executionMode, loadedTools))
                continue;

            Trace.TraceInformation($"[MCP] Remove saved {executionMode} path '{path}' because it loaded no tools.");
            configuredPaths.RemoveAt(index);
            removedAny = true;
        }

        return removedAny;
    }

    private static bool PathProducesTools(
        string path,
        ExecutionMode executionMode,
        IReadOnlyList<McpToolDefinition> loadedTools)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalizedPath = NormalizePath(path);
        return loadedTools.Any(tool => tool.SourceKind == executionMode &&
            IsToolFromConfiguredPath(normalizedPath, tool.SourcePath));
    }

    private static bool IsToolFromConfiguredPath(string configuredPath, string? toolSourcePath)
    {
        if (string.IsNullOrWhiteSpace(toolSourcePath))
            return false;

        var normalizedToolPath = NormalizePath(toolSourcePath!);
        if (string.Equals(configuredPath, normalizedToolPath, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!Directory.Exists(configuredPath))
            return false;

        var configuredWithSeparator = configuredPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return normalizedToolPath.StartsWith(configuredWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static RegistryInputKind ClassifyInputPath(string normalizedPath)
    {
        if (IsValidDotnetAssemblyPath(normalizedPath))
            return RegistryInputKind.DotnetAssembly;

        if (IsValidPythonToolsetPath(normalizedPath))
            return RegistryInputKind.PythonToolset;

        return RegistryInputKind.Unsupported;
    }

    private List<string> BuildDotnetCandidates(RegistryInputKind inputKind, string normalizedPath)
    {
        var candidates = settingsService.McpRegistryConfig.DotnetPaths.ToList();
        if (inputKind == RegistryInputKind.DotnetAssembly)
            AddDistinct(candidates, normalizedPath);
        return candidates;
    }

    private List<string> BuildPythonCandidates(RegistryInputKind inputKind, string normalizedPath)
    {
        var candidates = settingsService.McpRegistryConfig.PythonToolsetPaths.ToList();
        if (inputKind == RegistryInputKind.PythonToolset)
            AddDistinct(candidates, normalizedPath);
        return candidates;
    }

    private Task<IReadOnlyList<McpToolDefinition>> LoadCandidateToolsAsync(
        IReadOnlyList<string> dotnetCandidatePaths,
        IReadOnlyList<string> pythonCandidatePaths)
    {
        return Task.Run(() => ReloadTools(dotnetCandidatePaths, pythonCandidatePaths));
    }

    private void PersistAcceptedPath(
        RegistryInputKind inputKind,
        string normalizedPath,
        IReadOnlyList<McpToolDefinition> loadedTools)
    {
        switch (inputKind)
        {
            case RegistryInputKind.DotnetAssembly:
                PersistDotnetAssemblyPath(normalizedPath, loadedTools);
                break;
            case RegistryInputKind.PythonToolset:
                PersistPythonToolsetPath(normalizedPath, loadedTools);
                break;
        }
    }

    private void PersistDotnetAssemblyPath(string normalizedPath, IReadOnlyList<McpToolDefinition> loadedTools)
    {
        if (PathProducesTools(normalizedPath, ExecutionMode.Assembly, loadedTools))
        {
            AddDistinct(settingsService.McpRegistryConfig.DotnetPaths, normalizedPath);
            return;
        }

        Trace.TraceInformation($"[MCP] Skip persisting .NET assembly '{normalizedPath}' because it loaded no tools.");
    }

    private void PersistPythonToolsetPath(string normalizedPath, IReadOnlyList<McpToolDefinition> loadedTools)
    {
        if (PathProducesTools(normalizedPath, ExecutionMode.Python, loadedTools))
        {
            AddDistinct(settingsService.McpRegistryConfig.PythonToolsetPaths, normalizedPath);
            return;
        }

        Trace.TraceInformation($"[MCP] Skip persisting Python toolset '{normalizedPath}' because it loaded no tools.");
    }

    private static bool IsValidDotnetAssemblyPath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               File.Exists(path) &&
               string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidPythonToolsetPath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               Directory.Exists(path) &&
               ContainsPythonToolset(path!);
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool AddDistinct(List<string> paths, string path)
    {
        if (paths.Contains(path, StringComparer.OrdinalIgnoreCase))
            return false;

        paths.Add(path);
        return true;
    }

    private static bool ContainsPythonToolset(string directory)
    {
        return Directory.EnumerateFiles(directory, PythonToolPattern, SearchOption.AllDirectories).Any();
    }
}