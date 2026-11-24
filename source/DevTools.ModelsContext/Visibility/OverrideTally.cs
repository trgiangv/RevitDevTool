using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;

namespace DevTools.ModelsContext.Visibility;

public sealed class OverrideTally
{
    private readonly Dictionary<(int Length, int Group, int Middle, int Last), Node> _nodes = [];

    public static OverrideTally Count(DocumentSnapshot snapshot, IEnumerable<KeyValuePair<long, RowOverride>> overrides)
    {
        var tally = new OverrideTally();
        Span<int> path = stackalloc int[DocumentSnapshot.MaxGroupingDepth];
        foreach (var pair in overrides)
        {
            if (!snapshot.TryGetRow(pair.Key, out var row))
            {
                continue;
            }

            var length = snapshot.PathOf(row, path);
            for (var depth = 1; depth <= length; depth++)
            {
                tally.NodeAt(path[..depth]).Add(pair.Value);
            }
        }

        return tally;
    }

    public RowOverride? At(int[] keys, int total)
    {
        if (total <= 0 || !_nodes.TryGetValue(KeyOf(keys), out var node))
        {
            return null;
        }

        var color = node.Colored == total && !node.Mixed ? node.Color : null;
        var halftone = node.Halftone == total;
        return color is null && !halftone ? null : new RowOverride(color, halftone);
    }

    private static (int, int, int, int) KeyOf(ReadOnlySpan<int> keys)
    {
        return (keys.Length, keys.Length > 0 ? keys[0] : 0, keys.Length > 1 ? keys[1] : 0, keys.Length > 2 ? keys[2] : 0);
    }

    private Node NodeAt(ReadOnlySpan<int> keys)
    {
        var key = KeyOf(keys);
        if (!_nodes.TryGetValue(key, out var node))
        {
            node = new Node();
            _nodes.Add(key, node);
        }

        return node;
    }

    private sealed class Node
    {
        public int Colored { get; private set; }

        public int Halftone { get; private set; }

        public RgbColor? Color { get; private set; }

        public bool Mixed { get; private set; }

        public void Add(RowOverride value)
        {
            if (value.Halftone)
            {
                Halftone++;
            }

            if (value.Color is not { } color)
            {
                return;
            }

            Mixed |= Colored > 0 && Color != color;
            Color = color;
            Colored++;
        }
    }
}
