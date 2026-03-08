using System.Diagnostics;
using RevitDevTool.Mcp.Interfaces;
using RevitDevTool.Mcp.Schemas;
namespace RevitDevTool.Mcp;

public sealed class McpToolRegistry(IEnumerable<IMcpToolRegistryProvider> providers)
{
    private readonly Dictionary<string, McpToolDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<McpToolDefinition>> _definitionsByName = new(StringComparer.OrdinalIgnoreCase);
    private int LastLoadedCount { get; set; }
    private int LastSkippedCount { get; set; }
    private int LastConflictCount { get; set; }

    public IReadOnlyList<McpToolDefinition> GetTools()
    {
        return _definitions.Values
            .OrderBy(item => item.GroupName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool TryGetByToolId(string toolId, out McpToolDefinition? definition)
    {
        if (_definitions.TryGetValue(toolId, out var value))
        {
            definition = value;
            return true;
        }

        definition = null;
        return false;
    }

    public bool TryGetByName(string toolName, out McpToolDefinition? definition)
    {
        if (_definitionsByName.TryGetValue(toolName, out var matches) && matches.Count > 0)
        {
            definition = matches[0];
            return true;
        }

        definition = null;
        return false;
    }

    public void Reload()
    {
        _definitions.Clear();
        _definitionsByName.Clear();
        LastLoadedCount = 0;
        LastSkippedCount = 0;
        LastConflictCount = 0;

        foreach (var provider in providers.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var providerLoaded = 0;
                var providerSkipped = 0;
                var providerConflicted = 0;
                var loadedTools = provider.LoadTools();

                Trace.TraceInformation($"[MCP] Provider '{provider.Name}' returned {loadedTools.Count} tool candidate(s).");

                foreach (var definition in loadedTools.OrderBy(item => item.SourcePath, StringComparer.OrdinalIgnoreCase)
                             .ThenBy(item => item.ContainerType, StringComparer.OrdinalIgnoreCase)
                             .ThenBy(item => item.MethodName, StringComparer.OrdinalIgnoreCase))
                {
                    definition.EnsureIdentity();

                    if (string.IsNullOrWhiteSpace(definition.Name))
                    {
                        LastSkippedCount++;
                        providerSkipped++;
                        Trace.TraceWarning(
                            $"[MCP] Skip tool with empty name from provider='{provider.Name}', source='{definition.SourcePath}', container='{definition.ContainerType}', method='{definition.MethodName}', description='{definition.Description}'.");
                        continue;
                    }

                    if (_definitions.TryGetValue(definition.ToolId, out var existing))
                    {
                        LastConflictCount++;
                        LastSkippedCount++;
                        providerConflicted++;
                        providerSkipped++;
                        Trace.TraceWarning(
                            $"[MCP] Duplicate tool id '{definition.ToolId}' ignored. kept='{existing.SourcePath}:{existing.ContainerType}.{existing.MethodName}', skipped='{definition.SourcePath}:{definition.ContainerType}.{definition.MethodName}'.");
                        continue;
                    }

                    _definitions[definition.ToolId] = definition;
                    if (!_definitionsByName.TryGetValue(definition.Name, out var byName))
                    {
                        byName = [];
                        _definitionsByName[definition.Name] = byName;
                    }

                    byName.Add(definition);
                    LastLoadedCount++;
                    providerLoaded++;

                    if (byName.Count > 1)
                    {
                        Trace.TraceInformation(
                            $"[MCP] Tool name '{definition.Name}' has {byName.Count} registered variants. Use ToolId for deterministic routing.");
                    }
                }

                Trace.TraceInformation(
                    $"[MCP] Provider '{provider.Name}' summary. loaded={providerLoaded}, skipped={providerSkipped}, conflicted={providerConflicted}");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[MCP] Provider '{provider.Name}' failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        Trace.TraceInformation(
            $"[MCP] Registry reload complete. loaded={LastLoadedCount}, skipped={LastSkippedCount}, conflicted={LastConflictCount}");
    }
}
