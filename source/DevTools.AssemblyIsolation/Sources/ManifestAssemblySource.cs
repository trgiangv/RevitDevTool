using System.Reflection;
using DevTools.AssemblyIsolation.Identity;

namespace DevTools.AssemblyIsolation.Sources;

public sealed class ManifestAssemblySource : IManagedAssemblySource
{
    readonly IReadOnlyDictionary<string, IReadOnlyList<Entry>> entriesBySimpleName;

    public ManifestAssemblySource(IEnumerable<AssemblyCandidate> candidates)
        : this((candidates ?? throw new ArgumentNullException(nameof(candidates))).Select(candidate =>
            (AssemblyName.GetAssemblyName(candidate.Path), candidate)))
    {
    }

    public ManifestAssemblySource(IEnumerable<(AssemblyName Identity, AssemblyCandidate Candidate)> entries)
    {
        if (entries is null) throw new ArgumentNullException(nameof(entries));

        var grouped = new Dictionary<string, List<Entry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (identity, candidate) in entries)
        {
            if (identity is null) throw new ArgumentNullException(nameof(entries));
            if (candidate is null) throw new ArgumentNullException(nameof(entries));

            var simpleName = identity.Name
                ?? throw new ArgumentException("A manifest assembly identity must have a simple name.", nameof(entries));
            if (!grouped.TryGetValue(simpleName, out var group))
            {
                group = [];
                grouped.Add(simpleName, group);
            }

            if (group.Any(existing => HasSameFullIdentity(existing.Identity, identity)))
                continue;

            group.Add(new Entry(identity, candidate));
        }

        entriesBySimpleName = grouped.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<Entry>)Array.AsReadOnly(pair.Value.ToArray()),
            StringComparer.OrdinalIgnoreCase);
    }

    public AssemblyCandidate? Resolve(AssemblyName requested)
    {
        if (requested is null) throw new ArgumentNullException(nameof(requested));

        if (requested.Name is null || !entriesBySimpleName.TryGetValue(requested.Name, out var entries))
            return null;

        return entries.FirstOrDefault(entry => AssemblyIdentityMatcher.IsCompatible(requested, entry.Identity))?.Candidate;
    }

    static bool HasSameFullIdentity(AssemblyName first, AssemblyName second) =>
        AssemblyIdentityMatcher.IsCompatible(first, second)
        && AssemblyIdentityMatcher.IsCompatible(second, first);

    sealed class Entry
    {
        public Entry(AssemblyName identity, AssemblyCandidate candidate)
        {
            Identity = identity;
            Candidate = candidate;
        }

        public AssemblyName Identity { get; }

        public AssemblyCandidate Candidate { get; }
    }
}
