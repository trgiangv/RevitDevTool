using DevTools.ModelsContext.Documents;

namespace RevitDevTool.Tools.ModelsContext.Documents;

internal sealed class ScopeCache
{
    private readonly object _gate = new();
    private readonly Dictionary<(DocumentKey Document, long ViewId), HashSet<long>> _sets = [];

    public HashSet<long>? Find(DocumentKey document, long viewId)
    {
        lock (_gate)
        {
            return _sets.TryGetValue((document, viewId), out var ids) ? ids : null;
        }
    }

    public void Store(DocumentKey document, long viewId, HashSet<long> ids)
    {
        lock (_gate)
        {
            _sets[(document, viewId)] = ids;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _sets.Clear();
        }
    }
}
