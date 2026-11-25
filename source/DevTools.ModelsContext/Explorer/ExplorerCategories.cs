using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Visibility;
using System.Globalization;

namespace DevTools.ModelsContext.Explorer;

public sealed partial class ExplorerCategories : ObservableObject
{
    public const string NotReachedNote = "Links By Linked View or Custom keep their own settings";
    public const string SelectReason = "Select categories";
    public const string ParentHiddenReason = "Show its parent category first.";

    private readonly ContextBarViewModel _bar;
    private readonly ContextMenuViewModel _menu;
    private readonly Action<string?> _report;
    private readonly PanelRuntime _runtime;
    private readonly Func<CancellationToken> _token;
    private readonly VisibilityController _visibility;
    private IReadOnlyList<GroupCount> _groups = [];

    internal ExplorerCategories(
        PanelRuntime runtime,
        VisibilityController visibility,
        ContextBarViewModel bar,
        ContextMenuViewModel menu,
        Func<CancellationToken> token,
        Action changed,
        Action<string?> report)
    {
        _runtime = runtime;
        _visibility = visibility;
        _bar = bar;
        _menu = menu;
        _token = token;
        _report = report;
        Picker = new CategoryPickerViewModel(() => false, changed, checksNew: true);
        Picker.ChecksChanged += OnChecksChanged;
    }

    public CategoryPickerViewModel Picker { get; }

    public string? MenuReason => HasSelection() ? null : SelectReason;

    internal IReadOnlyList<long> Checked => Picker.Checked();

    public static string EyeTipOf(bool hidden, int links)
    {
        var verb = hidden ? "Show in view: " : "Hide in view: ";
        var scope = links switch
        {
            0 => "Current model",
            1 => "Current model + 1 link (By Host View)",
            _ => string.Format(CultureInfo.CurrentCulture, "Current model + {0:N0} links (By Host View)", links)
        };
        return links == 0 ? verb + scope : verb + scope + Environment.NewLine + NotReachedNote;
    }

    internal bool Lists(long groupId)
    {
        return Picker.Lists(groupId);
    }

    internal void Rebuild(ContextData data)
    {
        var groups = data.MergedGroups().ToDictionary(group => group.GroupId);
        if (data.Scope == ScopeKind.Selection)
        {
            AddCheckedDocuments(groups);
        }

        foreach (var document in data.Documents)
        {
            var snapshot = document.Snapshot;
            foreach (var group in snapshot.Groups.Where(group => _visibility.IsCategoryHidden(group.GroupId)))
            {
                if (!groups.ContainsKey(group.GroupId))
                {
                    groups[group.GroupId] = new GroupCount(group.GroupId, snapshot.Names[group.NameIndex], 0, group.ParentId);
                }
            }
        }

        Apply(groups.Values);
    }

    internal async Task ReadCheckedAsync(ContextData data, CancellationToken cancellationToken)
    {
        if (data.Scope != ScopeKind.Selection)
        {
            return;
        }

        foreach (var key in _bar.CheckedDocuments)
        {
            if (_runtime.Store.Find(key) is { IsLoaded: true } && _runtime.Store.TryGet(key) is null)
            {
                await _runtime.Store.GetAsync(key, null, cancellationToken);
            }
        }
    }

    internal void RebuildQuick(IReadOnlyList<QuickDocument> documents)
    {
        Apply(SnapshotAggregator.MergeGroups(documents.Select(document => document.Groups)));
    }

    private int LinkCount()
    {
        return _bar.Documents.Count(item => !item.Key.IsCurrentModel && item.Info.IsLoaded);
    }

    private void AddCheckedDocuments(Dictionary<long, GroupCount> groups)
    {
        foreach (var key in _bar.CheckedDocuments)
        {
            if (_runtime.Store.TryGet(key) is not { } snapshot)
            {
                continue;
            }

            foreach (var group in SnapshotAggregator.GroupsOf(snapshot, snapshot.Counts))
            {
                if (!groups.ContainsKey(group.GroupId))
                {
                    groups[group.GroupId] = group with { Count = 0 };
                }
            }
        }
    }

    private void Apply(IEnumerable<GroupCount> groups)
    {
        var links = LinkCount();
        _groups = CategoryTree.WithParents(groups.ToList(), Snapshots())
            .OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        Picker.Rebuild(_groups);
        foreach (var item in Picker.Items)
        {
            var hidden = _visibility.IsCategoryHidden(item.GroupId);
            item.IsHiddenInView = hidden;
            item.EyeToolTip = EyeTipOf(hidden, links);
            item.Eye = ToggleEyeAsync;
        }
    }

    private IEnumerable<DocumentSnapshot> Snapshots()
    {
        return _runtime.Data.Documents.Select(document => document.Snapshot)
            .Concat(_bar.CheckedDocuments.Select(key => _runtime.Store.TryGet(key)).OfType<DocumentSnapshot>());
    }

    private bool IsParentHidden(long groupId)
    {
        return _groups.FirstOrDefault(group => group.GroupId == groupId) is { ParentId: not GroupRow.TopLevel } child
               && _visibility.IsCategoryHidden(child.ParentId);
    }

    private List<TreeRow> EyeRowsOf(CategoryItemViewModel item)
    {
        var current = _runtime.Data.CurrentModel ?? _bar.CurrentModel;
        if (current is not { } key)
        {
            return [];
        }

        var rows = new List<TreeRow> { RowFactory.Category(key, item.GroupId, item.Name) };
        rows.AddRange(CategoryTree.ChildrenOf(_groups, item.GroupId)
            .Select(child => RowFactory.Category(key, child.GroupId, child.Name)));
        return rows;
    }

    private bool HasSelection()
    {
        return Picker.Items.Any(item => item.IsChecked);
    }

    private void OnChecksChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(MenuReason));
        IsolateCommand.NotifyCanExecuteChanged();
        HalftoneCommand.NotifyCanExecuteChanged();
        ColorCommand.NotifyCanExecuteChanged();
    }

    private List<TreeRow> SelectedRows()
    {
        var current = _runtime.Data.CurrentModel ?? _bar.CurrentModel;
        if (current is not { } key)
        {
            return [];
        }

        var names = _groups.ToDictionary(group => group.GroupId, group => group.Name);
        var checkedIds = Picker.Items.Where(item => item.IsChecked).Select(item => item.GroupId);
        return CategoryTree.WithChildren(_groups, checkedIds)
            .Select(id => RowFactory.Category(key, id, names.TryGetValue(id, out var name) ? name : string.Empty))
            .ToList();
    }

    private Task ToggleEyeAsync(CategoryItemViewModel item)
    {
        return _runtime.RunAsync(async () =>
        {
            var rows = EyeRowsOf(item);
            if (rows.Count == 0)
            {
                return;
            }

            var action = item.IsHiddenInView ? VisibilityAction.Show : VisibilityAction.Hide;
            if (action == VisibilityAction.Show && IsParentHidden(item.GroupId))
            {
                _report(ParentHiddenReason);
                return;
            }

            _report(await _visibility.RunAsync(VisibilityRequest.ForRows(action, rows)));
        });
    }

    private async Task RunSelectedAsync(VisibilityAction action)
    {
        var rows = SelectedRows();
        _report(rows.Count == 0
            ? SelectReason
            : await _visibility.RunAsync(VisibilityRequest.ForRows(action, rows)));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task IsolateAsync()
    {
        return _runtime.RunAsync(() => RunSelectedAsync(VisibilityAction.Isolate));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task HalftoneAsync()
    {
        return _runtime.RunAsync(() => RunSelectedAsync(VisibilityAction.Halftone));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ColorAsync()
    {
        var rows = SelectedRows();
        if (rows.Count == 0)
        {
            _report(SelectReason);
            return;
        }

        _menu.Open(new MenuTarget(rows, [], () => [], false, false, _token()));
        var color = _menu[MenuAction.Color];
        if (color.DisabledReason is { } reason)
        {
            _report(reason);
            return;
        }

        _report(null);
        await color.Command.ExecuteAsync(null);
    }
}
