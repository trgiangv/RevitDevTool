namespace DevTools.ModelsContext.Engine;

public enum ChangedKind
{
    LinkInstance,
    LinkType,
    ParameterElement,
    Family
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

public sealed class TrackedIds(IEnumerable<long> links, IEnumerable<long> parameters)
{
    public HashSet<long> Links { get; } = [.. links];

    public HashSet<long> Parameters { get; } = [.. parameters];
}

public sealed record ChangeOutcome(
    bool IsBulk,
    bool DefinitionsChanged,
    bool DocumentsChanged,
    IReadOnlyList<long> LinkTypes);

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
            return new ChangeOutcome(true, true, true, []);
        }

        var ids = tracked();
        var documents = false;
        var definitions = false;
        var linkTypes = new List<long>();
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
                case ChangedKind.ParameterElement or ChangedKind.Family:
                    definitions = true;
                    break;
            }
        }

        foreach (var deleted in report.Deleted)
        {
            documents |= ids.Links.Remove(deleted);
            definitions |= ids.Parameters.Remove(deleted);
        }

        return new ChangeOutcome(false, definitions, documents, linkTypes);
    }
}
