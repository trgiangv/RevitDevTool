using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Documents;
using System.Globalization;

namespace DevTools.ModelsContext.Filters;

public static class FilterNames
{
    public const string Prefix = "New filter";
    public const string EmptyName = "Enter a name.";
    public const string DuplicateName = "A custom filter with this name already exists.";

    public static string? Validate(string name, IEnumerable<string> others)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return EmptyName;
        }

        return others.Any(other => string.Equals(other.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
            ? DuplicateName
            : null;
    }

    public static string NextFree(IEnumerable<string> existing)
    {
        var taken = new HashSet<string>(existing.Select(name => name.Trim()), StringComparer.OrdinalIgnoreCase);
        for (var index = 1; ; index++)
        {
            var name = string.Format(CultureInfo.CurrentCulture, "{0} {1}", Prefix, index);
            if (!taken.Contains(name))
            {
                return name;
            }
        }
    }

    public static string Unique(string name, IEnumerable<string> existing)
    {
        var taken = new HashSet<string>(existing.Select(other => other.Trim()), StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(name))
        {
            return name;
        }

        for (var index = 2; ; index++)
        {
            var candidate = string.Format(CultureInfo.CurrentCulture, "{0} ({1})", name, index);
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}

public enum FilterSourceKind
{
    Custom,
    RuleBased,
    Selection
}

public enum ResultSelectionMode
{
    New,
    Add,
    Remove
}

public sealed partial class FilterSourceItem : ObservableObject
{
    public const string CustomGroup = "Custom filters";

    private FilterSourceItem(string name, string groupName, FilterSourceKind kind, HostFilterInfo? host, CustomFilter? saved)
    {
        Name = name;
        GroupName = groupName;
        Kind = kind;
        Host = host;
        Saved = saved;
    }

    [ObservableProperty]
    public partial string Name { get; internal set; }

    public string GroupName { get; }

    public FilterSourceKind Kind { get; }

    public bool IsCustom => Kind == FilterSourceKind.Custom;

    public string IconKey => Kind switch
    {
        FilterSourceKind.RuleBased => "Funnel",
        FilterSourceKind.Selection => "Cursor",
        _ => "CustomFilter"
    };

    public bool InView => Host?.InView == true;

    public DocumentKey? Document => Host?.Document;

    [ObservableProperty]
    public partial bool IsDirty { get; internal set; }

    internal HostFilterInfo? Host { get; }

    internal CustomFilter? Saved { get; set; }

    private string StoredName => Saved?.Name ?? Name;

    internal static FilterSourceItem Custom(CustomFilter? saved, string name)
    {
        return new FilterSourceItem(name, CustomGroup, FilterSourceKind.Custom, null, saved);
    }

    internal static FilterSourceItem ForHost(HostFilterInfo host, string groupName)
    {
        var kind = host.Kind == HostFilterKind.Selection ? FilterSourceKind.Selection : FilterSourceKind.RuleBased;
        return new FilterSourceItem(host.Name, groupName, kind, host, null);
    }

    internal bool SameAs(FilterSourceItem other)
    {
        return IsCustom
            ? other.IsCustom && string.Equals(StoredName, other.StoredName, StringComparison.OrdinalIgnoreCase)
            : other.Host is { } host && Host!.Document == host.Document && Host.Id == host.Id;
    }

    public override string ToString()
    {
        return Name;
    }
}

public sealed partial class CategoryItemViewModel : ObservableObject
{
    private readonly Action _changed;
    private readonly Action<CategoryItemViewModel> _expandedChanged;
    private readonly Action<CategoryItemViewModel>? _rowChecked;
    private readonly List<CategoryItemViewModel> _children = [];
    private bool _isChecked;
    private bool _isExpanded;
    private int _ownCount;

    internal CategoryItemViewModel(long groupId, string name, int count, Action changed,
        CategoryItemViewModel? parent = null, Action<CategoryItemViewModel>? expandedChanged = null,
        Action<CategoryItemViewModel>? rowChecked = null)
    {
        _rowChecked = rowChecked;
        GroupId = groupId;
        Name = name;
        _ownCount = count;
        Count = count;
        _changed = changed;
        Parent = parent;
        _expandedChanged = expandedChanged ?? (static _ => { });
        parent?.AddChild(this);
    }

    public long GroupId { get; }

    public string Name { get; }

    public CategoryItemViewModel? Parent { get; }

    public IReadOnlyList<CategoryItemViewModel> Children => _children;

    public bool IsChild => Parent is not null;

    public bool HasChildren => _children.Count > 0;

    [ObservableProperty]
    public partial int Count { get; private set; }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_rowChecked is null)
            {
                if (Check(value))
                {
                    _changed();
                }

                return;
            }

            if (SetChecked(value))
            {
                _rowChecked(this);
            }
        }
    }

    public bool? BoxState
    {
        get
        {
            if (_children.Count == 0)
            {
                return _isChecked;
            }

            var checkedCount = _children.Count(child => child.IsChecked) + (_isChecked ? 1 : 0);
            return checkedCount == 0 ? false : checkedCount == _children.Count + 1 ? true : null;
        }
        set
        {
            if (Check(value == true || (value == false && BoxState is null)))
            {
                _changed();
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetExpanded(value))
            {
                _expandedChanged(this);
            }
        }
    }

    [ObservableProperty]
    public partial bool IsHidden { get; internal set; }

    [ObservableProperty]
    public partial string? HideHint { get; internal set; }

    internal Func<CategoryItemViewModel, Task>? ToggleHidden { get; set; }

    internal long ParentId => Parent?.GroupId ?? GroupRow.TopLevel;

    private bool Check(bool value)
    {
        var changed = CheckChildren(value) | SetChecked(value);
        return Parent?.FollowChildren() is true || changed;
    }

    internal bool CheckChildren(bool value)
    {
        var changed = false;
        foreach (var child in _children)
        {
            changed |= child.SetChecked(value);
        }

        return changed;
    }

    internal bool FollowChildren()
    {
        if (_children.All(child => child.IsChecked))
        {
            return SetChecked(true);
        }

        return _children.All(child => !child.IsChecked) && SetChecked(false);
    }

    internal bool SetChecked(bool value)
    {
        if (!SetProperty(ref _isChecked, value, nameof(IsChecked)))
        {
            return false;
        }

        OnPropertyChanged(nameof(BoxState));
        Parent?.OnPropertyChanged(nameof(BoxState));
        return true;
    }

    internal bool SetExpanded(bool value)
    {
        return SetProperty(ref _isExpanded, value, nameof(IsExpanded));
    }

    internal void SetOwnCount(int count)
    {
        _ownCount = count;
    }

    internal void RefreshCount()
    {
        Count = _ownCount + _children.Sum(child => child._ownCount);
    }

    private void AddChild(CategoryItemViewModel child)
    {
        _children.Add(child);
    }

    [RelayCommand]
    private Task ToggleHiddenAsync()
    {
        return ToggleHidden?.Invoke(this) ?? Task.CompletedTask;
    }

    public override string ToString()
    {
        return Name;
    }
}
