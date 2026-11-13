using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.ViewModels;

public sealed class DocumentScope(
    DocumentInfo info,
    string label,
    DocumentSnapshot snapshot,
    IReadOnlyCollection<long>? ids)
{
    private HashSet<long>? _idSet;

    public DocumentInfo Info { get; } = info;

    public DocumentKey Key => Info.Key;

    public string Label { get; } = label;

    public DocumentSnapshot Snapshot { get; } = snapshot;

    public IReadOnlyCollection<long>? Ids { get; } = ids;

    public CountNode Counts { get; } = ids is null ? snapshot.Counts : ScopeCounter.Count(snapshot, ids);

    public bool InScope(long id)
    {
        if (Ids is null)
        {
            return Snapshot.Contains(id);
        }

        _idSet ??= Ids as HashSet<long> ?? [.. Ids];
        return _idSet.Contains(id);
    }

    public IEnumerable<long> IdsUnder(RowPath path)
    {
        if (Ids is null)
        {
            return Snapshot.IdsUnder(path);
        }

        return Ids.Where(id => Snapshot.TryGetRow(id, out var row) && Snapshot.IsUnder(row, path));
    }
}

public sealed class ContextData(ScopeKind scope, DocumentKey? currentModel, IReadOnlyList<DocumentScope> documents)
{
    public static ContextData Empty { get; } = new(ScopeKind.ActiveView, null, []);

    public ScopeKind Scope { get; } = scope;

    public DocumentKey? CurrentModel { get; } = currentModel;

    public IReadOnlyList<DocumentScope> Documents { get; } = documents;

    public DocumentScope? Find(DocumentKey key)
    {
        return Documents.FirstOrDefault(document => document.Key == key);
    }

    public IReadOnlyList<(long GroupId, string Name, int Count)> MergedGroups()
    {
        var merged = new Dictionary<long, (string Name, int Count)>();
        foreach (var document in Documents)
        {
            foreach (var node in document.Counts.Children.Values)
            {
                var group = document.Snapshot.Groups[node.Key];
                merged[group.GroupId] = merged.TryGetValue(group.GroupId, out var entry)
                    ? (entry.Name, entry.Count + node.Count)
                    : (document.Snapshot.Names[group.NameIndex], node.Count);
            }
        }

        return merged
            .Select(pair => (pair.Key, pair.Value.Name, pair.Value.Count))
            .OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
