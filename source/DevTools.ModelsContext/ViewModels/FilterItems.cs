using CommunityToolkit.Mvvm.ComponentModel;
using DevTools.ModelsContext.Model;
using System.Globalization;

namespace DevTools.ModelsContext.ViewModels;

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
            ? other.IsCustom && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
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
    private bool _isChecked;

    internal CategoryItemViewModel(long groupId, string name, int count, Action changed)
    {
        GroupId = groupId;
        Name = name;
        Count = count;
        _changed = changed;
    }

    public long GroupId { get; }

    public string Name { get; }

    [ObservableProperty]
    public partial int Count { get; internal set; }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetProperty(ref _isChecked, value))
            {
                _changed();
            }
        }
    }

    internal void SetChecked(bool value)
    {
        SetProperty(ref _isChecked, value, nameof(IsChecked));
    }

    public override string ToString()
    {
        return Name;
    }
}
