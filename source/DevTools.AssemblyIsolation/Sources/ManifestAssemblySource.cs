using System.Reflection;
using DevTools.AssemblyIsolation.Identity;

namespace DevTools.AssemblyIsolation.Sources;

public sealed class ManifestAssemblySource : IManagedAssemblySource
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<Entry>> entriesBySimpleName;

    public ManifestAssemblySource(IEnumerable<AssemblyCandidate> candidates)
        : this((candidates ?? throw new ArgumentNullException(nameof(candidates))).Select(candidate =>
            (AssemblyName.GetAssemblyName(candidate.Path), candidate)))
    {
    }

    public ManifestAssemblySource(IEnumerable<(AssemblyName Identity, AssemblyCandidate Candidate)> entries)
    {
        if (entries is null) throw new ArgumentNullException(nameof(entries));

        entriesBySimpleName = BuildEntries(entries).ToDictionary(
            static pair => pair.Key,
            static IReadOnlyList<Entry> (pair) => Array.AsReadOnly(pair.Value.ToArray()),
            StringComparer.OrdinalIgnoreCase);
    }

    public AssemblyCandidate? Resolve(AssemblyName requested)
    {
        if (requested is null) throw new ArgumentNullException(nameof(requested));

        return requested.Name is null || !entriesBySimpleName.TryGetValue(requested.Name, out var entries)
            ? null
            : Resolve(requested, entries);
    }

    private static AssemblyCandidate? Resolve(AssemblyName requested, IReadOnlyList<Entry> entries)
    {
        var exact = entries.FirstOrDefault(entry =>
            AssemblyIdentityMatcher.IsCompatible(requested, entry.Identity));
        if (exact is not null)
            return exact.Candidate;

#if NETFRAMEWORK
        return ResolveAllowed(requested, entries)?.Candidate;
#else
        return null;
#endif
    }

#if NETFRAMEWORK
    private static Entry? ResolveAllowed(AssemblyName requested, IReadOnlyList<Entry> entries)
    {
        Entry? newest = null;
        foreach (var entry in entries)
        {
            if (!NetfxBclBind.AllowsNewer(requested, entry.Identity))
                continue;

            if (IsNewerThanCurrent(entry, newest))
                newest = entry;
        }

        return newest;
    }

    private static bool IsNewerThanCurrent(Entry candidate, Entry? current) =>
        current is null
        || candidate.Identity.Version is not null
        && (current.Identity.Version is null || candidate.Identity.Version > current.Identity.Version);
#endif

    private static bool HasSameFullIdentity(AssemblyName first, AssemblyName second) =>
        AssemblyIdentityMatcher.IsCompatible(first, second)
        && AssemblyIdentityMatcher.IsCompatible(second, first);

    private static Dictionary<string, List<Entry>> BuildEntries(IEnumerable<(AssemblyName Identity, AssemblyCandidate Candidate)> entries)
    {
        var grouped = new Dictionary<string, List<Entry>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (identity, candidate) in entries)
            AddEntry(grouped, identity, candidate);

        return grouped;
    }

    private static void AddEntry(
        Dictionary<string, List<Entry>> grouped,
        AssemblyName? identity,
        AssemblyCandidate? candidate)
    {
        if (identity is null) throw new ArgumentNullException(nameof(identity));
        if (candidate is null) throw new ArgumentNullException(nameof(candidate));

        var simpleName = identity.Name
            ?? throw new ArgumentException("A manifest assembly identity must have a simple name.", nameof(identity));
        var group = GetOrCreateGroup(grouped, simpleName);

        if (group.Any(existing => HasSameFullIdentity(existing.Identity, identity)))
            return;

        group.Add(new Entry(identity, candidate));
    }

    private static List<Entry> GetOrCreateGroup(Dictionary<string, List<Entry>> grouped, string simpleName)
    {
        if (grouped.TryGetValue(simpleName, out var group))
            return group;

        group = [];
        grouped.Add(simpleName, group);
        return group;
    }

    private sealed class Entry(AssemblyName identity, AssemblyCandidate candidate)
    {
        public AssemblyName Identity { get; } = identity;

        public AssemblyCandidate Candidate { get; } = candidate;
    }
}
