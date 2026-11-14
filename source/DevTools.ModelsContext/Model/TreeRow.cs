using System.ComponentModel;
using System.Globalization;

namespace DevTools.ModelsContext.Model;

public sealed class TreeRow : INotifyPropertyChanged
{
    public const double IndentPerLevel = 14;

    private static readonly PropertyChangedEventArgs HighlightChanged = new(nameof(IsHighlighted));
    private static readonly PropertyChangedEventArgs CountTextChanged = new(nameof(CountText));
    private static readonly PropertyChangedEventArgs BadgeChanged = new(nameof(Badge));
    private static readonly PropertyChangedEventArgs ExpandedChanged = new(nameof(IsExpanded));
    private static readonly PropertyChangedEventArgs HasChildrenChanged = new(nameof(HasChildren));
    private static readonly PropertyChangedEventArgs EyeChanged = new(nameof(Eye));
    private static readonly PropertyChangedEventArgs DisabledReasonChanged = new(nameof(DisabledReason));
    private static readonly PropertyChangedEventArgs IsEnabledChanged = new(nameof(IsEnabled));

    private int _count;
    private string _countText = "0";
    private int _selectedInside;
    private bool _isExpanded;
    private bool _hasChildren;
    private RowEye _eye;
    private string? _disabledReason;
    private bool _isHighlighted;

    public TreeRow(
        TreeRowKind kind,
        string text,
        TreeRow? parent = null,
        DocumentKey? document = null,
        RowPath? path = null,
        long id = RowPath.Unset)
    {
        Kind = kind;
        Text = text;
        Parent = parent;
        Document = document ?? parent?.Document;
        Path = path ?? RowPath.None;
        Id = id;
        Level = parent is null ? 0 : parent.Level + 1;
        Indent = Level * IndentPerLevel;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public TreeRowKind Kind { get; }

    public string Text { get; }

    public TreeRow? Parent { get; }

    public DocumentKey? Document { get; }

    public RowPath Path { get; }

    public long Id { get; }

    public int Level { get; }

    public double Indent { get; }

    public int Count
    {
        get => _count;
        set
        {
            if (_count != value)
            {
                _count = value;
                _countText = value.ToString("N0", CultureInfo.CurrentCulture);
                Raise(CountTextChanged);
            }
        }
    }

    public string CountText => _countText;

    public int SelectedInside
    {
        get => _selectedInside;
        set
        {
            if (_selectedInside != value)
            {
                var badge = Badge;
                _selectedInside = value;
                RaiseBadgeIfChanged(badge);
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                var badge = Badge;
                _isExpanded = value;
                Raise(ExpandedChanged);
                RaiseBadgeIfChanged(badge);
            }
        }
    }

    public bool HasChildren
    {
        get => _hasChildren;
        set
        {
            if (_hasChildren != value)
            {
                _hasChildren = value;
                Raise(HasChildrenChanged);
            }
        }
    }

    public IReadOnlyList<TreeRow>? Children { get; set; }

    public RowEye Eye
    {
        get => _eye;
        set
        {
            if (_eye != value)
            {
                _eye = value;
                Raise(EyeChanged);
            }
        }
    }

    public string Key { get; init; } = string.Empty;

    public string? DisabledReason
    {
        get => _disabledReason;
        set
        {
            if (_disabledReason != value)
            {
                var wasEnabled = IsEnabled;
                _disabledReason = value;
                Raise(DisabledReasonChanged);
                if (wasEnabled != IsEnabled)
                {
                    Raise(IsEnabledChanged);
                }
            }
        }
    }

    public bool IsEnabled => _disabledReason is null;

    public bool HasCount => Kind is not (TreeRowKind.Instance or TreeRowKind.MorePage);

    public int Badge => _isExpanded ? 0 : _selectedInside;

    public bool ChildrenLoaded => Children is not null;

    public bool IsHeader => Kind is TreeRowKind.AllModelsHeader or TreeRowKind.CurrentModelHeader;

    public bool IsInsideLink => Document is { IsCurrentModel: false } && Kind != TreeRowKind.LinkModel;

    public bool IsHighlighted
    {
        get => _isHighlighted;
        internal set
        {
            if (_isHighlighted != value)
            {
                _isHighlighted = value;
                Raise(HighlightChanged);
            }
        }
    }

    private void RaiseBadgeIfChanged(int before)
    {
        if (Badge != before)
        {
            Raise(BadgeChanged);
        }
    }

    private void Raise(PropertyChangedEventArgs args)
    {
        PropertyChanged?.Invoke(this, args);
    }
}
