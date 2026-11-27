using DevTools.ModelsContext.Documents;

namespace RevitDevTool.Tools.ModelsContext.Documents;

internal sealed class ScopeCache
{
    private readonly object _lock = new();
    private readonly Dictionary<(DocumentKey Document, long ViewId), HashSet<long>> _sets = [];

    public HashSet<long>? Find(DocumentKey document, long viewId)
    {
        lock (_lock)
        {
            return _sets.TryGetValue((document, viewId), out var ids) ? ids : null;
        }
    }

    public void Store(DocumentKey document, long viewId, HashSet<long> ids)
    {
        lock (_lock)
        {
            _sets[(document, viewId)] = ids;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _sets.Clear();
        }
    }
}
