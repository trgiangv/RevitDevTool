using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using System.Collections.ObjectModel;
using System.Globalization;

namespace DevTools.ModelsContext.Scope;

public sealed partial class CategoryPickerViewModel : ObservableObject
{
    public const string AllCategories = "All categories";
    public const string NoCategories = "None";

    private readonly Action _changed;
    private readonly HashSet<long> _excluded = [];
    private readonly HashSet<long> _expanded = [];
    private readonly HashSet<long> _closedInSearch = [];
    private readonly List<CategoryItemViewModel> _rowBatch = [];
    private readonly Func<bool> _isReadOnly;
    private readonly bool _ticksNew;
    private readonly Func<bool> _emptyMatchesAll;

    internal CategoryPickerViewModel(Func<bool> isReadOnly, Action changed, bool ticksNew = false,
        Func<bool>? emptyMatchesAll = null)
    {
        _isReadOnly = isReadOnly;
        _changed = changed;
        _ticksNew = ticksNew;
        _emptyMatchesAll = emptyMatchesAll ?? (static () => true);
    }

    public ObservableCollection<CategoryItemViewModel> Items { get; } = [];

    public event EventHandler? TicksChanged;

    public string Search
    {
        get;
        set
        {
            if (SetProperty(ref field, value ?? string.Empty))
            {
                _closedInSearch.Clear();
                UpdateVisible();
            }
        }
    } = string.Empty;

    public ObservableCollection<CategoryItemViewModel> VisibleItems { get; } = [];

    public string Summary => _ticksNew ? TickSummary() : PickSummary();

    internal IReadOnlyList<long> Ids => Items.Select(item => item.GroupId).ToList();

    internal IReadOnlyList<long> Checked()
    {
        return Items.Where(item => item.IsChecked).Select(item => item.GroupId).ToList();
    }


    internal bool Lists(long groupId)
    {
        return !_excluded.Contains(groupId);
    }

    internal string? Label(long groupId)
    {
        return Items.FirstOrDefault(item => item.GroupId == groupId)?.Name;
    }

    internal void Rebuild(IReadOnlyList<GroupCount> groups)
    {
        var ordered = TreeOrder(groups);
        if (ordered.Select(group => (group.GroupId, group.ParentId))
            .SequenceEqual(Items.Select(item => (item.GroupId, item.ParentId))))
        {
            for (var index = 0; index < ordered.Count; index++)
            {
                Items[index].SetOwnCount(ordered[index].Count);
            }

            RefreshCounts();
            return;
        }

        var checkedIds = Checked().ToHashSet();
        var byId = new Dictionary<long, CategoryItemViewModel>();
        Items.Clear();
        foreach (var group in ordered)
        {
            var parent = group.ParentId != GroupRow.TopLevel && byId.TryGetValue(group.ParentId, out var found) ? found : null;
            var item = new CategoryItemViewModel(group.GroupId, group.Name, group.Count, OnItemChanged, parent,
                OnExpandedChanged, OnRowTicked);
            item.SetChecked(_ticksNew ? Lists(group.GroupId) : checkedIds.Contains(group.GroupId));
            item.SetExpanded(_expanded.Contains(group.GroupId));
            byId[group.GroupId] = item;
            Items.Add(item);
        }

        RefreshCounts();
        UpdateVisible();
        RaiseTicks();
    }

    private static List<GroupCount> TreeOrder(IReadOnlyList<GroupCount> groups)
    {
        var ids = groups.Select(group => group.GroupId).ToHashSet();
        bool IsRoot(GroupCount group) => group.ParentId == GroupRow.TopLevel || group.ParentId == group.GroupId
                                         || !ids.Contains(group.ParentId);
        var roots = groups.Where(IsRoot).ToList();
        var rootIds = roots.Select(group => group.GroupId).ToHashSet();
        var children = groups.Where(group => !IsRoot(group) && rootIds.Contains(group.ParentId))
            .ToLookup(group => group.ParentId);
        var orphans = groups.Where(group => !IsRoot(group) && !rootIds.Contains(group.ParentId))
            .Select(group => group with { ParentId = GroupRow.TopLevel });
        var ordered = new List<GroupCount>();
        foreach (var root in roots.Concat(orphans))
        {
            ordered.Add(root);
            ordered.AddRange(children[root.GroupId]);
        }

        return ordered;
    }

    private void RefreshCounts()
    {
        foreach (var item in Items)
        {
            item.RefreshCount();
        }
    }

    private void OnRowTicked(CategoryItemViewModel item)
    {
        var first = _rowBatch.Count == 0;
        if (!_rowBatch.Contains(item))
        {
            _rowBatch.Add(item);
        }

        if (!first)
        {
            return;
        }

        if (SynchronizationContext.Current is { } context)
        {
            context.Post(_ => SettleRows(), null);
        }
        else
        {
            SettleRows();
        }
    }

    private void SettleRows()
    {
        var batch = _rowBatch.ToHashSet();
        _rowBatch.Clear();
        foreach (var parent in batch.Where(item => item.HasChildren))
        {
            if (parent.IsChecked || !parent.Children.Any(batch.Contains))
            {
                parent.TickChildren(parent.IsChecked);
            }
        }

        foreach (var parent in batch.Where(item => item.Parent is not null && !batch.Contains(item.Parent))
                     .Select(item => item.Parent!).Distinct())
        {
            if (batch.Count > 1 && parent.IsChecked && parent.Children.All(child => batch.Contains(child) && !child.IsChecked))
            {
                parent.TickChildren(true);
            }
            else
            {
                parent.FollowChildren();
            }
        }

        OnItemChanged();
    }

    private void OnExpandedChanged(CategoryItemViewModel item)
    {
        if (item.IsExpanded)
        {
            _expanded.Add(item.GroupId);
            _closedInSearch.Remove(item.GroupId);
        }
        else
        {
            _expanded.Remove(item.GroupId);
            _closedInSearch.Add(item.GroupId);
        }

        UpdateVisible();
    }

    internal void Check(IEnumerable<long> groups)
    {
        var ids = groups.ToHashSet();
        foreach (var item in Items)
        {
            item.SetChecked(ids.Contains(item.GroupId));
        }

        RaiseTicks();
    }

    public void SetTicks(IEnumerable<CategoryItemViewModel> ticked, IEnumerable<CategoryItemViewModel> unticked)
    {
        if (_isReadOnly())
        {
            return;
        }

        var changed = false;
        foreach (var item in ticked.Where(item => !item.IsChecked))
        {
            item.SetChecked(true);
            changed = true;
        }

        foreach (var item in unticked.Where(item => item.IsChecked))
        {
            item.SetChecked(false);
            changed = true;
        }

        if (changed)
        {
            OnItemChanged();
        }
    }

    [RelayCommand]
    private void CheckAll()
    {
        CheckVisible(true);
    }

    [RelayCommand]
    private void CheckNone()
    {
        CheckVisible(false);
    }

    private void CheckVisible(bool value)
    {
        if (_isReadOnly())
        {
            return;
        }

        var listed = VisibleItems.Concat(VisibleItems.Where(item => item.HasChildren && !item.IsExpanded)
            .SelectMany(item => item.Children)).ToList();
        var changed = false;
        foreach (var item in listed.Where(item => item.IsChecked != value).ToList())
        {
            item.SetChecked(value);
            changed = true;
        }

        if (changed)
        {
            OnItemChanged();
        }
    }

    private void OnItemChanged()
    {
        if (_ticksNew)
        {
            foreach (var item in Items)
            {
                if (item.IsChecked)
                {
                    _excluded.Remove(item.GroupId);
                }
                else
                {
                    _excluded.Add(item.GroupId);
                }
            }
        }

        RaiseTicks();
        _changed();
    }

    private void RaiseTicks()
    {
        OnPropertyChanged(nameof(Summary));
        TicksChanged?.Invoke(this, EventArgs.Empty);
    }

    private string PickSummary()
    {
        var names = Items.Where(item => item.IsChecked).Select(item => item.Name).ToList();
        if (names.Count > 0)
        {
            return string.Join(", ", names);
        }

        return _emptyMatchesAll() ? AllCategories : NoCategories;
    }

    internal void RefreshSummary()
    {
        OnPropertyChanged(nameof(Summary));
    }

    private string TickSummary()
    {
        var ticked = Items.Count(item => item.IsChecked);
        if (ticked == Items.Count)
        {
            return AllCategories;
        }

        return ticked == 0
            ? NoCategories
            : string.Format(CultureInfo.CurrentCulture, "{0:N0} of {1:N0}", ticked, Items.Count);
    }

    private void UpdateVisible()
    {
        var text = Search.Trim();
        var visible = new List<CategoryItemViewModel>();
        foreach (var root in Items.Where(item => !item.IsChild))
        {
            var self = text.Length == 0 || Matches(root, text);
            var children = self ? root.Children : root.Children.Where(child => Matches(child, text)).ToList();
            if (!self && children.Count == 0)
            {
                continue;
            }

            root.SetExpanded(_expanded.Contains(root.GroupId)
                             || (!self && !_closedInSearch.Contains(root.GroupId)));
            visible.Add(root);
            if (root.IsExpanded)
            {
                visible.AddRange(children);
            }
        }

        Show(visible);
    }

    private void Show(List<CategoryItemViewModel> visible)
    {
        var keep = visible.ToHashSet();
        for (var index = VisibleItems.Count - 1; index >= 0; index--)
        {
            if (!keep.Contains(VisibleItems[index]))
            {
                VisibleItems.RemoveAt(index);
            }
        }

        for (var index = 0; index < visible.Count; index++)
        {
            if (index >= VisibleItems.Count || VisibleItems[index] != visible[index])
            {
                VisibleItems.Insert(index, visible[index]);
            }
        }
    }

    private static bool Matches(CategoryItemViewModel item, string text)
    {
        return item.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
