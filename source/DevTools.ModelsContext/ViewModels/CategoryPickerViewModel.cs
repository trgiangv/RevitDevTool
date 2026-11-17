using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace DevTools.ModelsContext.ViewModels;

public sealed partial class CategoryPickerViewModel : ObservableObject
{
    public const string AllCategories = "All categories";

    private readonly Action _changed;
    private readonly Func<bool> _isReadOnly;

    internal CategoryPickerViewModel(Func<bool> isReadOnly, Action changed)
    {
        _isReadOnly = isReadOnly;
        _changed = changed;
    }

    public ObservableCollection<CategoryItemViewModel> Items { get; } = [];

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

    public string Summary
    {
        get
        {
            var names = Items.Where(item => item.IsChecked).Select(item => item.Name).ToList();
            return names.Count == 0 ? AllCategories : string.Join(", ", names);
        }
    }

    internal IReadOnlyList<long> Ids => Items.Select(item => item.GroupId).ToList();

    internal IReadOnlyList<long> Checked()
    {
        return Items.Where(item => item.IsChecked).Select(item => item.GroupId).ToList();
    }

    internal string? Label(long groupId)
    {
        return Items.FirstOrDefault(item => item.GroupId == groupId)?.Name;
    }

    internal void Rebuild(IReadOnlyList<(long GroupId, string Name, int Count)> groups)
    {
        var checkedIds = Checked().ToHashSet();
        Items.Clear();
        foreach (var group in groups)
        {
            var item = new CategoryItemViewModel(group.GroupId, group.Name, group.Count, OnItemChanged);
            item.SetChecked(checkedIds.Contains(group.GroupId));
            Items.Add(item);
        }

        UpdateVisible();
        OnPropertyChanged(nameof(Summary));
    }

    internal void Check(IEnumerable<long> groups)
    {
        var ids = groups.ToHashSet();
        foreach (var item in Items)
        {
            item.SetChecked(ids.Contains(item.GroupId));
        }

        OnPropertyChanged(nameof(Summary));
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
        OnPropertyChanged(nameof(Summary));
        _changed();
    }

    private void UpdateVisible()
    {
        var text = Search.Trim();
        VisibleItems = text.Length == 0
            ? Items.ToList()
            : Items.Where(item => item.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
    }
}
