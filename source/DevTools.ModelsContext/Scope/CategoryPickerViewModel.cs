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
    private readonly Func<bool> _isReadOnly;
    private readonly bool _ticksNew;

    internal CategoryPickerViewModel(Func<bool> isReadOnly, Action changed, bool ticksNew = false)
    {
        _isReadOnly = isReadOnly;
        _changed = changed;
        _ticksNew = ticksNew;
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
                UpdateVisible();
            }
        }
    } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<CategoryItemViewModel> VisibleItems { get; private set; } = [];

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
        if (groups.Select(group => group.GroupId).SequenceEqual(Items.Select(item => item.GroupId)))
        {
            for (var index = 0; index < groups.Count; index++)
            {
                Items[index].Count = groups[index].Count;
            }

            return;
        }

        var checkedIds = Checked().ToHashSet();
        Items.Clear();
        foreach (var group in groups)
        {
            var item = new CategoryItemViewModel(group.GroupId, group.Name, group.Count, OnItemChanged);
            item.SetChecked(_ticksNew ? Lists(group.GroupId) : checkedIds.Contains(group.GroupId));
            Items.Add(item);
        }

        UpdateVisible();
        RaiseTicks();
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

        var changed = false;
        foreach (var item in VisibleItems.Where(item => item.IsChecked != value))
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
        return names.Count == 0 ? AllCategories : string.Join(", ", names);
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
        VisibleItems = text.Length == 0
            ? Items.ToList()
            : Items.Where(item => item.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
    }
}
