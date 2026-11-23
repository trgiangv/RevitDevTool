namespace DevTools.ModelsContext.Documents;

public enum ChangedKind
{
    LinkInstance,
    LinkType,
    ParameterElement,
    Family,
    ElementType,
    DesignOption,
    Unknown
}

public enum CacheDrop
{
    Values,
    Resolved,
    Candidates,
    All
}

public readonly record struct ChangedId(long Id, ChangedKind Kind);

public sealed record ChangeReport(
    int Added,
    int Modified,
    IReadOnlyList<ChangedId> AddedKinds,
    IReadOnlyList<ChangedId> ModifiedKinds,
    IReadOnlyList<long> Deleted)
{
    public bool IsBulk => ChangeClassifier.IsBulk(Added, Modified, Deleted.Count);
}

public sealed class TrackedIds(IEnumerable<long> links, IEnumerable<long> parameters, IEnumerable<long>? families = null)
{
    public HashSet<long> Links { get; } = [.. links];

    public HashSet<long> Parameters { get; } = [.. parameters];

    public HashSet<long> Families { get; } = [.. families ?? []];
}

public sealed record ChangeOutcome(
    bool IsBulk,
    bool DefinitionsChanged,
    bool DocumentsChanged,
    IReadOnlyList<long> LinkTypes,
    CacheDrop Drop);

public static class ChangeClassifier
{
    public static bool IsBulk(int added, int modified, int deleted)
    {
        return added + modified + deleted > DeltaApplier.DirtyThreshold;
    }

    public static ChangeOutcome Classify(ChangeReport report, Func<TrackedIds> tracked)
    {
        if (report.IsBulk)
        {
            return new ChangeOutcome(true, true, true, [], CacheDrop.All);
        }

        var ids = tracked();
        var documents = false;
        var definitions = false;
        var linkTypes = new List<long>();
        var options = false;
        var types = false;
        foreach (var added in report.AddedKinds)
        {
            switch (added.Kind)
            {
                case ChangedKind.LinkInstance:
                    ids.Links.Add(added.Id);
                    documents = true;
                    break;
                case ChangedKind.LinkType:
                    documents = true;
                    break;
                case ChangedKind.ParameterElement:
                    ids.Parameters.Add(added.Id);
                    definitions = true;
                    break;
                case ChangedKind.Family:
                    ids.Families.Add(added.Id);
                    definitions = true;
                    break;
            }
        }

        foreach (var modified in report.ModifiedKinds)
        {
            switch (modified.Kind)
            {
                case ChangedKind.LinkType:
                    linkTypes.Add(modified.Id);
                    break;
                case ChangedKind.ParameterElement or ChangedKind.Family or ChangedKind.Unknown:
                    definitions = true;
                    break;
                case ChangedKind.DesignOption:
                    options = true;
                    break;
                case ChangedKind.ElementType:
                    types = true;
                    break;
            }
        }

        foreach (var deleted in report.Deleted)
        {
            documents |= ids.Links.Remove(deleted);
            definitions |= ids.Parameters.Remove(deleted);
            definitions |= ids.Families.Remove(deleted);
        }

        return new ChangeOutcome(false, definitions, documents, linkTypes,
            DropOf(report, definitions, options, types));
    }

    public static bool KeepsResolved(CacheDrop drop, int resolved, IEnumerable<long> modified,
        Func<long, bool>? inCandidates)
    {
        return drop == CacheDrop.Values && resolved > 0 && inCandidates is not null && !modified.Any(inCandidates);
    }

    private static CacheDrop DropOf(ChangeReport report, bool definitions, bool options, bool types)
    {
        if (definitions)
        {
            return CacheDrop.All;
        }

        if (report.Added > 0 || report.Deleted.Count > 0 || options)
        {
            return CacheDrop.Candidates;
        }

        return types ? CacheDrop.Resolved : CacheDrop.Values;
    }
}
