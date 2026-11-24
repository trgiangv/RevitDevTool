using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Scope;

public static class ScopeCounter
{
    public static CountNode Count(DocumentSnapshot snapshot, IEnumerable<long> ids)
    {
        var root = new CountNode(-1);
        Span<int> path = stackalloc int[DocumentSnapshot.MaxGroupingDepth];
        foreach (var id in ids)
        {
            if (!snapshot.TryGetRow(id, out var row))
            {
                continue;
            }

            var length = snapshot.PathOf(row, path);
            root.Add(path[..length], 1);
        }

        return root;
    }
}
