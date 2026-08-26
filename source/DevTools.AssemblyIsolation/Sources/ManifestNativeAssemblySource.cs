namespace DevTools.AssemblyIsolation.Sources;

public sealed class ManifestNativeAssemblySource : INativeAssemblySource
{
    readonly IReadOnlyDictionary<string, AssemblyCandidate> candidatesByName;

    public ManifestNativeAssemblySource(IEnumerable<AssemblyCandidate> candidates)
    {
        if (candidates is null) throw new ArgumentNullException(nameof(candidates));

        var indexed = new Dictionary<string, AssemblyCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (candidate is null) throw new ArgumentNullException(nameof(candidates));
            foreach (var key in AssemblyCandidate.LookupKeys(candidate.Path))
            {
                if (indexed.TryGetValue(key, out var existing)
                    && !string.Equals(existing.Path, candidate.Path, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Manifest contains ambiguous native asset '{key}'.");
                }

                indexed[key] = candidate;
            }
        }

        candidatesByName = indexed;
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
