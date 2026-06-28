namespace DevTools.Mcp.Routing.Catalog;

public sealed class DynamicToolCatalog
{
    private readonly Lock _gate = new();
    private IReadOnlyList<DynamicToolCatalogEntry> _registrations = [];

    public void ReplaceSnapshot(IEnumerable<DynamicToolCatalogEntry> registrations)
    {
        var snapshot = registrations
            .OrderBy(item => item.Tool.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Instance.ProcessId)
            .ToArray();

        lock (_gate)
            _registrations = snapshot;
    }

    public IReadOnlyList<DynamicToolCatalogEntry> List()
    {
        lock (_gate)
            return _registrations.ToArray();
    }

    public DynamicToolResolution Resolve(string toolName, int? hostInstanceId)
    {
        DynamicToolCatalogEntry[] candidates;
        lock (_gate)
        {
            candidates = _registrations
                .Where(item => string.Equals(item.Tool.Name, toolName, StringComparison.OrdinalIgnoreCase))
                .Where(item => hostInstanceId is null || item.Instance.ProcessId == hostInstanceId)
                .ToArray();
        }

        return candidates.Length switch
        {
            0 => new DynamicToolResolution(DynamicToolResolutionState.NotFound, null, candidates),
            1 => new DynamicToolResolution(DynamicToolResolutionState.Found, candidates[0], candidates),
            _ => new DynamicToolResolution(DynamicToolResolutionState.Ambiguous, null, candidates)
        };
    }
}
