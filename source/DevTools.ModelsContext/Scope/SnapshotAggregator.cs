using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Scope;

public static class SnapshotAggregator
{
    public static CountNode Aggregate(DocumentSnapshot snapshot)
    {
        var root = new CountNode(-1);
        Span<int> path = stackalloc int[DocumentSnapshot.MaxGroupingDepth];
        foreach (var row in snapshot.Rows)
        {
            if (row.SecondaryOption)
            {
                continue;
            }

            var length = snapshot.PathOf(row, path);
            root.Add(path[..length], 1);
        }

        return root;
    }

    public static IReadOnlyList<GroupCount> GroupsOf(DocumentSnapshot snapshot, CountNode counts)
    {
        return counts.Children.Values
            .Select(node =>
            {
                var group = snapshot.Groups[node.Key];
                return new GroupCount(group.GroupId, snapshot.Names[group.NameIndex], node.Count, group.ParentId);
            })
            .ToList();
    }

    public static IReadOnlyList<GroupCount> MergeGroups(IEnumerable<IEnumerable<GroupCount>> documents)
    {
        var merged = new Dictionary<long, GroupCount>();
        foreach (var group in documents.SelectMany(groups => groups))
        {
            merged[group.GroupId] = merged.TryGetValue(group.GroupId, out var entry)
                ? entry with { Count = entry.Count + group.Count }
                : group;
        }

        return merged.Values
            .OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
