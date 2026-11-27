using DevTools.ModelsContext.Documents;
using System.Diagnostics.CodeAnalysis;

namespace DevTools.ModelsContext.Visibility;

internal sealed class DocumentIds
{
    private readonly Dictionary<DocumentKey, HashSet<long>> _ids = [];

    public void Add(DocumentKey document, IEnumerable<long> ids)
    {
        if (!_ids.TryGetValue(document, out var set))
        {
            set = [];
            _ids.Add(document, set);
        }

        set.UnionWith(ids);
    }

    public bool TryGetValue(DocumentKey document, [MaybeNullWhen(false)] out HashSet<long> ids)
    {
        return _ids.TryGetValue(document, out ids);
    }

    public void Remove(DocumentKey document)
    {
        _ids.Remove(document);
    }

    public void Clear()
    {
        _ids.Clear();
    }

    public void Rename(ModelRename rename)
    {
        var pairs = _ids.ToList();
        _ids.Clear();
        foreach (var pair in pairs)
        {
            _ids[rename.Map(pair.Key)] = pair.Value;
        }
    }
}
