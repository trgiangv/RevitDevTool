using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Engine;

public enum ReloadOutcome
{
    Kept,
    Patched,
    Rebuilt
}

public static class VersionRule
{
    public const double RebuildShare = 0.2;

    public static bool IsSame(DocumentVersion? stamp, DocumentVersion current)
    {
        return stamp is { } known && known.IsSameAs(current);
    }

    public static bool IsTooLarge(ElementDelta delta, int elementCount)
    {
        return delta.ChangedCount > Math.Max(1, elementCount) * RebuildShare;
    }

    public static bool IsReflected(DocumentSnapshot snapshot, ElementDelta delta)
    {
        if (delta.Deleted.Any(snapshot.Contains))
        {
            return false;
        }

        foreach (var element in delta.Added.Concat(delta.Modified))
        {
            if (!snapshot.TryGetRow(element.Id, out var row)
                || snapshot.Types[row.TypeIndex].TypeId != element.TypeId
                || row.LevelId != element.LevelId)
            {
                return false;
            }
        }

        return true;
    }
}
