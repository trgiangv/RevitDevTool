namespace DevTools.ModelsContext.Documents;

public enum DeltaOutcome
{
    Applied,
    Dirty,
    Ignored
}

public static class DeltaApplier
{
    public const int DirtyThreshold = 2000;

    public static DeltaOutcome Apply(DocumentSnapshot snapshot, ElementDelta delta, int dirtyThreshold = DirtyThreshold)
    {
        if (delta.Source != snapshot.Source)
        {
            return DeltaOutcome.Ignored;
        }

        if (snapshot.IsDirty)
        {
            return DeltaOutcome.Dirty;
        }

        if (delta.ChangedCount <= dirtyThreshold && TryApply(snapshot, delta))
        {
            return DeltaOutcome.Applied;
        }

        snapshot.MarkDirty();
        return DeltaOutcome.Dirty;
    }

    private static bool TryApply(DocumentSnapshot snapshot, ElementDelta delta)
    {
        if (!TryUpsertTypes(snapshot, delta.Types))
        {
            return false;
        }

        foreach (var id in delta.Deleted)
        {
            snapshot.Remove(id);
        }

        return TryUpsertElements(snapshot, delta.Added) && TryUpsertElements(snapshot, delta.Modified);
    }

    private static bool TryUpsertTypes(DocumentSnapshot snapshot, IReadOnlyList<TypeRecord> types)
    {
        foreach (var type in types)
        {
            snapshot.UpsertType(type, out var moved);
            if (moved)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryUpsertElements(DocumentSnapshot snapshot, IReadOnlyList<ElementRecord> elements)
    {
        foreach (var element in elements)
        {
            var known = snapshot.Contains(element.Id)
                ? snapshot.TryReplace(element)
                : snapshot.TryAdd(element);
            if (!known)
            {
                return false;
            }
        }

        return true;
    }
}
