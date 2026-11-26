using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;

namespace DevTools.ModelsContext.Scope;

public sealed class DocumentScope(
    DocumentInfo info,
    string label,
    DocumentSnapshot snapshot,
    IReadOnlyCollection<long>? ids)
{
    private HashSet<long>? _idSet;
    private HashSet<long>? _outside;
    private ViewInfo? _outsideView;

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
            return Snapshot.InModel(id);
        }

        _idSet ??= Ids as HashSet<long> ?? [.. Ids];
        return _idSet.Contains(id);
    }

    public HashSet<long> OutsideIds(ViewInfo? view)
    {
        if (_outside is not null && _outsideView == view)
        {
            return _outside;
        }

        _outside = [];
        _outsideView = view;
        if (Ids is null)
        {
            return _outside;
        }

        foreach (var row in Snapshot.Rows)
        {
            if (!row.SecondaryOption && !InScope(row.Id) && ViewDraws.CanDraw(view, row, Key.IsCurrentModel))
            {
                _outside.Add(row.Id);
            }
        }

        return _outside;
    }

    public IEnumerable<long> IdsUnder(RowPath path)
    {
        if (Ids is null)
        {
            return Snapshot.ModelIdsUnder(path);
        }

        return Ids.Where(id => Snapshot.TryGetRow(id, out var row) && Snapshot.IsUnder(row, path));
    }

    public IEnumerable<long> IdsOf(TreeRow row)
    {
        return row.Kind switch
        {
            TreeRowKind.Instance => [row.Id],
            TreeRowKind.Group => Snapshot.WithSubgroups(row.Path).SelectMany(IdsUnder),
            TreeRowKind.Family or TreeRowKind.Type => IdsUnder(row.Path),
            TreeRowKind.CurrentModelHeader => row.Branches.SelectMany(IdsUnder),
            _ => []
        };
    }

    public IReadOnlyList<GroupCount> Groups()
    {
        return SnapshotAggregator.GroupsOf(Snapshot, Counts);
    }
}

public sealed class ContextData(
    ScopeKind scope,
    DocumentKey? currentModel,
    IReadOnlyList<DocumentScope> documents,
    IReadOnlyList<DocumentInfo>? known = null)
{
    public static ContextData Empty { get; } = new(ScopeKind.ActiveView, null, []);

    public ScopeKind Scope { get; } = scope;

    public DocumentKey? CurrentModel { get; } = currentModel;

    public IReadOnlyList<DocumentScope> Documents { get; } = documents;

    public IReadOnlyList<DocumentInfo> Known { get; } = known ?? documents.Select(document => document.Info).ToList();

    public HashSet<SourceId> KnownSources { get; } =
        [.. (known ?? documents.Select(document => document.Info)).Select(info => info.Source)];

    public DocumentScope? Find(DocumentKey key)
    {
        return Documents.FirstOrDefault(document => document.Key == key);
    }

    public IReadOnlyList<GroupCount> MergedGroups()
    {
        return SnapshotAggregator.MergeGroups(Documents.Select(document => document.Groups()));
    }

    public IReadOnlyList<ElementRef> ElementsOf(IEnumerable<TreeRow> rows)
    {
        var elements = new HashSet<ElementRef>();
        foreach (var row in rows)
        {
            switch (row.Kind)
            {
                case TreeRowKind.LinkModel when row.Document is { } link:
                    elements.Add(new ElementRef(link.CurrentModel, link.LinkInstanceId));
                    break;
                default:
                    if (row.Document is { } key && Find(key) is { } scope)
                    {
                        elements.UnionWith(scope.IdsOf(row).Select(id => new ElementRef(key, id)));
                    }

                    break;
            }
        }

        return elements.ToList();
    }
}
