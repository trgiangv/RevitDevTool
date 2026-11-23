using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;

namespace DevTools.ModelsContext.Visibility;

public sealed class MarkTally
{
    private readonly Dictionary<(int Length, int Group, int Middle, int Last), Node> _nodes = [];

    public static MarkTally Count(DocumentSnapshot snapshot, IEnumerable<KeyValuePair<long, RowOverride>> marks)
    {
        var tally = new MarkTally();
        Span<int> path = stackalloc int[TreeSchema.MaxGroupingDepth];
        foreach (var mark in marks)
        {
            if (!snapshot.TryGetRow(mark.Key, out var row))
            {
                continue;
            }

            var length = snapshot.PathOf(row, path);
            for (var depth = 1; depth <= length; depth++)
            {
                tally.NodeAt(path[..depth]).Add(mark.Value);
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

        public void Add(RowOverride mark)
        {
            if (mark.Halftone)
            {
                Halftone++;
            }

            if (mark.Color is not { } color)
            {
                return;
            }

            Mixed |= Colored > 0 && Color != color;
            Color = color;
            Colored++;
        }
    }
}
