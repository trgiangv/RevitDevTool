namespace DevTools.ModelsContext.Scope;

public sealed class CountNode(int key)
{
    private static readonly Dictionary<int, CountNode> Empty = [];

    private Dictionary<int, CountNode>? _children;

    public int Key { get; } = key;

    public int Count { get; private set; }

    public IReadOnlyDictionary<int, CountNode> Children => _children ?? Empty;

    public CountNode? Find(params int[] path)
    {
        var node = this;
        foreach (var key in path)
        {
            if (node._children is null || !node._children.TryGetValue(key, out var child))
            {
                return null;
            }

            node = child;
        }

        return node;
    }

    public int CountAt(params int[] path)
    {
        return Find(path)?.Count ?? 0;
    }

    internal void Add(ReadOnlySpan<int> path, int amount)
    {
        Count += amount;
        if (path.IsEmpty)
        {
            return;
        }

        _children ??= [];
        if (!_children.TryGetValue(path[0], out var child))
        {
            child = new CountNode(path[0]);
            _children.Add(path[0], child);
        }

        child.Add(path[1..], amount);
        if (child.Count <= 0)
        {
            _children.Remove(path[0]);
        }
    }
}
