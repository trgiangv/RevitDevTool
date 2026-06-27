namespace DevTools.Mcp.Routing;

public sealed class DynamicToolCatalog
{
    private readonly Lock _gate = new();
    private IReadOnlyList<DynamicToolRegistration> _registrations = [];

    public void ReplaceSnapshot(IEnumerable<DynamicToolRegistration> registrations)
    {
        var snapshot = registrations
            .OrderBy(item => item.Tool.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Instance.ProcessId)
            .ToArray();

        lock (_gate)
            _registrations = snapshot;
    }

    public IReadOnlyList<DynamicToolRegistration> List()
    {
        lock (_gate)
            return _registrations.ToArray();
    }

    public DynamicToolResolution Resolve(string toolName, int? hostInstanceId)
    {
        DynamicToolRegistration[] candidates;
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
