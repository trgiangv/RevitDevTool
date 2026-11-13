using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Engine;

public sealed record MergedGroup(long GroupId, string Name, int Count, IReadOnlyList<DocumentKey> Documents);

public static class SnapshotAggregator
{
    public static CountNode Aggregate(DocumentSnapshot snapshot)
    {
        var root = new CountNode(-1);
        Span<int> path = stackalloc int[TreeSchema.MaxGroupingDepth];
        foreach (var row in snapshot.Rows)
        {
            var length = snapshot.PathOf(row, path);
            root.Add(path[..length], 1);
        }

        return root;
    }

    public static IReadOnlyList<MergedGroup> MergeGroups(
        IEnumerable<KeyValuePair<DocumentKey, DocumentSnapshot>> documents)
    {
        var merged = new Dictionary<long, (string Name, int Count, List<DocumentKey> Documents)>();
        foreach (var pair in documents)
        {
            var snapshot = pair.Value;
            foreach (var node in snapshot.Counts.Children.Values)
            {
                var group = snapshot.Groups[node.Key];
                if (!merged.TryGetValue(group.GroupId, out var entry))
                {
                    entry = (snapshot.Names[group.NameIndex], 0, []);
                }

                entry.Documents.Add(pair.Key);
                merged[group.GroupId] = (entry.Name, entry.Count + node.Count, entry.Documents);
            }
        }

        return merged
            .Select(pair => new MergedGroup(pair.Key, pair.Value.Name, pair.Value.Count, pair.Value.Documents))
            .OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
