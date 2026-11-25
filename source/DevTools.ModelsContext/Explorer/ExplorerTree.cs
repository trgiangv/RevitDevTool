using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;

namespace DevTools.ModelsContext.Explorer;

internal sealed class ExplorerTree(PanelRuntime runtime, DocumentStates states, ExplorerCategories categories)
{
    private readonly Dictionary<TreeRow, long[]> _instanceIds = [];

    public List<TreeRow> Roots { get; private set; } = [];

    public CancellationToken Token { get; set; }

    private ContextData Data => runtime.Data;

    public void Clear()
    {
        Roots = [];
        _instanceIds.Clear();
    }

    public void Build()
    {
        Roots = Data.Documents.OrderBy(document => !document.Key.IsCurrentModel)
            .Select(document => RowFactory.RootRow(document.Key, document.Label))
            .ToList();
    }

    public List<TreeRow> ShowQuickCounts(IReadOnlyList<QuickDocument> documents, ExplorerState state)
    {
        _instanceIds.Clear();
        Roots = QuickRoots(documents);
        var visible = new List<TreeRow>();
        foreach (var root in Roots)
        {
            root.IsExpanded = state.Expanded.Contains(root.Key) || !state.Roots.Contains(root.Key);
            visible.Add(root);
            if (root.IsExpanded)
            {
                visible.AddRange(root.Children!);
            }
        }

        return visible;
    }

    public DocumentScope? DocumentOf(TreeRow row)
    {
        return row.Document is { } key ? Data.Find(key) : null;
    }

    public async Task EnsureChildrenAsync(TreeRow row)
    {
        if (row.ChildrenLoaded)
        {
            return;
        }

        var scope = DocumentOf(row);
        var depth = scope?.Snapshot.GroupingDepth ?? 0;
        row.Children = row.Kind switch
        {
            TreeRowKind.CurrentModelHeader or TreeRowKind.LinkModel when scope is not null => GroupRows(row, scope),
            TreeRowKind.Group when depth == 3 && scope is not null => FamilyRows(row, scope),
            TreeRowKind.Group when depth == 2 && scope is not null => TypeRows(row, scope),
            TreeRowKind.Family when scope is not null => TypeRows(row, scope),
            TreeRowKind.Group or TreeRowKind.Type when scope is not null => await InstanceRowsAsync(row, scope),
            _ => []
        };
        if (row.Subgroups is { Count: > 0 } subgroups && (row.Children.Count == 0 || row.Children[0] != subgroups[0]))
        {
            row.Children = [.. subgroups, .. row.Children];
        }
    }

    public async Task<IReadOnlyList<TreeRow>> AppendPageAsync(TreeRow parent)
    {
        if (!_instanceIds.TryGetValue(parent, out var ids) || parent.Document is not { } key)
        {
            return [];
        }

        var existing = (parent.Children ?? []).Where(child => child.Kind == TreeRowKind.Instance).ToList();
        var take = ids.Skip(existing.Count).Take(ExplorerTabViewModel.InstancePageSize).ToList();
        var names = await runtime.Services.Source.GetInstanceNamesAsync(key, take, Token);
        var page = names.Select(name => RowFactory.Instance(parent, key, name)).ToList();
        var shown = existing.Count + take.Count;
        if (shown < ids.Length)
        {
            page.Add(RowFactory.MorePage(parent, key, shown, ids.Length));
        }

        parent.Children = (parent.Subgroups ?? []).Concat(existing).Concat(page).ToList();
        return page;
    }

    public async Task ExpandBranchAsync(TreeRow row)
    {
        if (!row.HasChildren || HoldsInstances(row))
        {
            return;
        }

        await EnsureChildrenAsync(row);
        row.IsExpanded = true;
        foreach (var child in row.Children!)
        {
            await ExpandBranchAsync(child);
        }
    }

    public async Task RestoreAsync(TreeRow row, HashSet<string> expanded)
    {
        if (!row.HasChildren)
        {
            return;
        }

        await EnsureChildrenAsync(row);
        row.IsExpanded = true;
        foreach (var child in row.Children!.Where(child => expanded.Contains(child.Key)))
        {
            await RestoreAsync(child, expanded);
        }
    }

    public HashSet<string> ExpandedKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        Visit(Roots, row =>
        {
            if (row.IsExpanded)
            {
                keys.Add(row.Key);
            }
        });
        return keys;
    }

    public HashSet<string> RootKeys()
    {
        return new HashSet<string>(Roots.Select(root => root.Key), StringComparer.Ordinal);
    }

    public void CollapseAll()
    {
        Visit(Roots, row => row.IsExpanded = false);
    }

    public void ClearBadges()
    {
        Visit(Roots, row => row.SelectedInside = 0);
    }

    private static void Visit(IEnumerable<TreeRow> rows, Action<TreeRow> action)
    {
        foreach (var row in rows)
        {
            action(row);
            if (row.Children is { } children)
            {
                Visit(children, action);
            }
        }
    }

    private List<TreeRow> QuickRoots(IReadOnlyList<QuickDocument> documents)
    {
        var roots = new List<TreeRow>();
        foreach (var document in documents.OrderBy(document => !document.Key.IsCurrentModel))
        {
            var groups = document.Groups.Where(group => categories.Lists(group.GroupId)).ToList();
            var root = RowFactory.RootRow(document.Key, document.Label);
            root.Children = RowFactory.QuickGroupRows(root, document.Key, groups);
            root.Count = groups.Sum(group => group.Count);
            roots.Add(root);
        }

        return roots;
    }

    private bool HoldsInstances(TreeRow row)
    {
        return row.Kind == TreeRowKind.Type
               || (row.Kind == TreeRowKind.Group && (DocumentOf(row)?.Snapshot.GroupingDepth ?? 0) < 2);
    }

    private List<TreeRow> GroupRows(TreeRow parent, DocumentScope scope)
    {
        var snapshot = scope.Snapshot;
        var state = states[scope.Key];
        var listed = RowFactory.KeysOf(scope.Counts, state.Hidden, state.Unseen)
            .Where(index => categories.Lists(snapshot.Groups[index].GroupId))
            .ToList();
        var tops = listed.Where(index => !snapshot.Groups[index].IsSubgroup)
            .Select(index => snapshot.Groups[index].GroupId).ToHashSet();
        bool Nested(int index) => snapshot.Groups[index] is { IsSubgroup: true } group && tops.Contains(group.ParentId);
        var rows = RowFactory.Sorted(listed.Where(index => !Nested(index)).Select(index => GroupRow(parent, scope, index)));
        var nested = listed.Where(Nested).ToLookup(index => snapshot.Groups[index].ParentId);
        foreach (var row in rows.Where(row => nested.Contains(row.Id)))
        {
            row.Subgroups = RowFactory.Sorted(nested[row.Id].Select(index => GroupRow(row, scope, index)));
        }

        return rows;
    }

    private static TreeRow GroupRow(TreeRow parent, DocumentScope scope, int index)
    {
        var groupId = scope.Snapshot.Groups[index].GroupId;
        return new TreeRow(TreeRowKind.Group, scope.Snapshot.GroupName(index), parent, path: RowPath.ForGroup(index),
            id: groupId)
        {
            HasChildren = true,
            Key = RowFactory.RowKey(TreeRowKind.Group, scope.Key, groupId)
        };
    }

    private List<TreeRow> FamilyRows(TreeRow parent, DocumentScope scope)
    {
        var keys = RowFactory.KeyPath(parent.Path, 3);
        var group = parent.Path.Group;
        var groupId = scope.Snapshot.Groups[group].GroupId;
        var state = states[scope.Key];
        return RowFactory.Sorted(RowFactory.KeysOf(scope.Counts.Find(keys), state.Hidden.Find(keys),
                state.Unseen.Find(keys))
            .Select(family =>
                new TreeRow(TreeRowKind.Family, scope.Snapshot.Names[family], parent,
                    path: RowPath.ForFamily(group, family))
                {
                    HasChildren = true,
                    Key = RowFactory.RowKey(TreeRowKind.Family, scope.Key, groupId, scope.Snapshot.Names[family])
                }));
    }

    private List<TreeRow> TypeRows(TreeRow parent, DocumentScope scope)
    {
        var snapshot = scope.Snapshot;
        var keys = RowFactory.KeyPath(parent.Path, snapshot.GroupingDepth);
        var groupId = snapshot.Groups[parent.Path.Group].GroupId;
        var family = parent.Path.Family == RowPath.Any ? string.Empty : snapshot.Names[parent.Path.Family];
        var state = states[scope.Key];
        return RowFactory.Sorted(RowFactory.KeysOf(scope.Counts.Find(keys), state.Hidden.Find(keys),
                state.Unseen.Find(keys))
            .Select(type =>
                new TreeRow(TreeRowKind.Type, snapshot.TypeName(type), parent,
                    path: RowPath.ForType(parent.Path.Group, parent.Path.Family, type))
                {
                    HasChildren = true,
                    Key = RowFactory.RowKey(TreeRowKind.Type, scope.Key, groupId, family, snapshot.Types[type].TypeId)
                }));
    }

    private async Task<IReadOnlyList<TreeRow>> InstanceRowsAsync(TreeRow parent, DocumentScope scope)
    {
        var state = states[scope.Key];
        var snapshot = scope.Snapshot;
        var ids = new HashSet<long>(scope.IdsUnder(parent.Path));
        foreach (var id in state.HiddenIds.Concat(state.UnseenIds))
        {
            if (snapshot.TryGetRow(id, out var row) && snapshot.IsUnder(row, parent.Path))
            {
                ids.Add(id);
            }
        }

        _instanceIds[parent] = [.. ids.OrderBy(id => id)];
        parent.Children = [];
        return await AppendPageAsync(parent);
    }
}
