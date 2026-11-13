using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Engine;

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

        if (delta.ChangedCount > dirtyThreshold)
        {
            snapshot.MarkDirty();
            return DeltaOutcome.Dirty;
        }

        foreach (var type in delta.Types)
        {
            snapshot.UpsertType(type, out var moved);
            if (moved)
            {
                snapshot.MarkDirty();
                return DeltaOutcome.Dirty;
            }
        }

        foreach (var id in delta.Deleted)
        {
            snapshot.Remove(id);
        }

        foreach (var element in delta.Added.Concat(delta.Modified))
        {
            var known = snapshot.Contains(element.Id)
                ? snapshot.TryReplace(element)
                : snapshot.TryAdd(element);
            if (!known)
            {
                snapshot.MarkDirty();
                return DeltaOutcome.Dirty;
            }
        }

        return DeltaOutcome.Applied;
    }
}
