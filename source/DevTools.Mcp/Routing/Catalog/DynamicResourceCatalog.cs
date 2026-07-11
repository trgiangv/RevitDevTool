namespace DevTools.Mcp.Routing.Catalog;

public sealed class DynamicResourceCatalog
{
    private readonly Lock _gate = new();
    private IReadOnlyList<DynamicResourceCatalogEntry> _registrations = [];

    public void ReplaceSnapshot(IEnumerable<DynamicResourceCatalogEntry> registrations)
    {
        var snapshot = registrations
            .OrderBy(item => item.Uri, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Instance.ProcessId)
            .ToArray();

        lock (_gate)
            _registrations = snapshot;
    }

    public IReadOnlyList<DynamicResourceCatalogEntry> List()
    {
        lock (_gate)
            return _registrations.ToArray();
    }

    public DynamicResourceResolution Resolve(string uri, int? hostInstanceId)
    {
        DynamicResourceCatalogEntry[] candidates;
        lock (_gate)
        {
            candidates = _registrations
                .Where(item => string.Equals(item.Uri, uri, StringComparison.OrdinalIgnoreCase))
                .Where(item => hostInstanceId is null || item.Instance.ProcessId == hostInstanceId)
                .ToArray();
        }

        return candidates.Length switch
        {
            0 => new DynamicResourceResolution(DynamicResolutionState.NotFound, null, candidates),
            1 => new DynamicResourceResolution(DynamicResolutionState.Found, candidates[0], candidates),
            _ => new DynamicResourceResolution(DynamicResolutionState.Ambiguous, null, candidates)
        };
    }
}
