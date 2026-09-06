namespace DevTools.AssemblyIsolation.Sources;

public sealed class ManifestNativeAssemblySource : INativeAssemblySource
{
    private readonly Dictionary<string, AssemblyCandidate> candidatesByName;

    public ManifestNativeAssemblySource(IEnumerable<AssemblyCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var indexed = new Dictionary<string, AssemblyCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
            AddCandidate(indexed, candidate);

        candidatesByName = indexed;
    }

    private static void AddCandidate(Dictionary<string, AssemblyCandidate> indexed, AssemblyCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        foreach (var key in AssemblyCandidate.LookupKeys(candidate.Path))
        {
            if (HasConflictingCandidate(indexed, key, candidate))
                throw new InvalidOperationException($"Manifest contains ambiguous native asset '{key}'.");

            indexed[key] = candidate;
        }
    }

    private static bool HasConflictingCandidate(
        Dictionary<string, AssemblyCandidate> indexed,
        string key,
        AssemblyCandidate candidate)
    {
        return indexed.TryGetValue(key, out var existing)
            && !string.Equals(existing.Path, candidate.Path, StringComparison.OrdinalIgnoreCase);
    }

    public AssemblyCandidate? Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        foreach (var key in AssemblyCandidate.LookupKeys(name))
        {
            if (candidatesByName.TryGetValue(key, out var candidate))
                return candidate;
        }

        return null;
    }
}
