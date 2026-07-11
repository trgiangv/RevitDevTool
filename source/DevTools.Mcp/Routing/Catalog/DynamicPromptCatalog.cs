namespace DevTools.Mcp.Routing.Catalog;

public sealed class DynamicPromptCatalog
{
    private readonly Lock _gate = new();
    private IReadOnlyList<DynamicPromptCatalogEntry> _registrations = [];

    public void ReplaceSnapshot(IEnumerable<DynamicPromptCatalogEntry> registrations)
    {
        var snapshot = registrations
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Instance.ProcessId)
            .ToArray();

        lock (_gate)
            _registrations = snapshot;
    }

    public IReadOnlyList<DynamicPromptCatalogEntry> List()
    {
        lock (_gate)
            return _registrations.ToArray();
    }

    public DynamicPromptResolution Resolve(string name, int? hostInstanceId)
    {
        DynamicPromptCatalogEntry[] candidates;
        lock (_gate)
        {
            candidates = _registrations
                .Where(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
                .Where(item => hostInstanceId is null || item.Instance.ProcessId == hostInstanceId)
                .ToArray();
        }

        return candidates.Length switch
        {
            0 => new DynamicPromptResolution(DynamicResolutionState.NotFound, null, candidates),
            1 => new DynamicPromptResolution(DynamicResolutionState.Found, candidates[0], candidates),
            _ => new DynamicPromptResolution(DynamicResolutionState.Ambiguous, null, candidates)
        };
    }
}
