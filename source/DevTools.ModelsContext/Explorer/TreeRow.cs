using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Visibility;
using System.ComponentModel;
using System.Globalization;

namespace DevTools.ModelsContext.Explorer;

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
    private static readonly PropertyChangedEventArgs OverrideColorChanged = new(nameof(OverrideColor));
    private static readonly PropertyChangedEventArgs ColorHexChanged = new(nameof(ColorHex));
    private static readonly PropertyChangedEventArgs HasColorChanged = new(nameof(HasColor));
    private static readonly PropertyChangedEventArgs HalftoneChanged = new(nameof(IsHalftone));
    private static readonly PropertyChangedEventArgs HiddenCountChanged = new(nameof(HiddenCount));
    private static readonly PropertyChangedEventArgs HiddenTextChanged = new(nameof(HiddenText));
    private static readonly PropertyChangedEventArgs HasHiddenChanged = new(nameof(HasHidden));
    private static readonly PropertyChangedEventArgs DimmedChanged = new(nameof(IsDimmed));
    private static readonly PropertyChangedEventArgs HiddenInViewChanged = new(nameof(IsHiddenInView));

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
        get;
        set
        {
            if (field != value)
            {
                field = value;
                CountText = value.ToString("N0", CultureInfo.CurrentCulture);
                Raise(CountTextChanged);
            }
        }
    }

    public string CountText { get; private set; } = "0";

    public int SelectedInside
    {
        get;
        set
        {
            if (field != value)
            {
                var badge = Badge;
                field = value;
                RaiseBadgeIfChanged(badge);
            }
        }
    }

    public bool IsExpanded
    {
        get;
        set
        {
            if (field != value)
            {
                var badge = Badge;
                field = value;
                Raise(ExpandedChanged);
                RaiseBadgeIfChanged(badge);
            }
        }
    }

    public bool HasChildren
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                Raise(HasChildrenChanged);
            }
        }
    }

    public IReadOnlyList<TreeRow>? Children { get; set; }

    public IReadOnlyList<TreeRow>? Subgroups { get; internal set; }

    public RowEye Eye
    {
        get;
        set
        {
            if (field != value)
            {
                var saved = IsHiddenInView;
                field = value;
                Raise(EyeChanged);
                if (saved != IsHiddenInView)
                {
                    Raise(HiddenInViewChanged);
                }
            }
        }
    }

    public string Key { get; init; } = string.Empty;

    public string? DisabledReason
    {
        get;
        set
        {
            if (field != value)
            {
                var wasEnabled = IsEnabled;
                field = value;
                Raise(DisabledReasonChanged);
                if (wasEnabled != IsEnabled)
                {
                    Raise(IsEnabledChanged);
                }
            }
        }
    }

    public bool IsEnabled => DisabledReason is null;

    public bool HasCount => Kind is not (TreeRowKind.Instance or TreeRowKind.MorePage);

    public int Badge => IsExpanded ? 0 : SelectedInside;

    public bool ChildrenLoaded => Children is not null;

    public bool IsHeader => Kind == TreeRowKind.CurrentModelHeader;

    public int HiddenCount
    {
        get;
        internal set
        {
            if (field != value)
            {
                var had = HasHidden;
                field = value;
                HiddenText = value > 0
                    ? string.Format(CultureInfo.CurrentCulture, "+{0:N0} hidden", value)
                    : string.Empty;
                Raise(HiddenCountChanged);
                Raise(HiddenTextChanged);
                if (had != HasHidden)
                {
                    Raise(HasHiddenChanged);
                }
            }
        }
    }

    public string HiddenText { get; private set; } = string.Empty;

    public bool HasHidden => HiddenCount > 0;

    public bool IsDimmed
    {
        get;
        internal set
        {
            if (field != value)
            {
                field = value;
                Raise(DimmedChanged);
            }
        }
    }

    public bool IsInsideLink => Document is { IsCurrentModel: false } && Kind != TreeRowKind.LinkModel;

    public bool IsHiddenInView => IsInsideLink && Eye is RowEye.Hidden or RowEye.Partly;

    public bool IsHighlighted
    {
        get;
        internal set
        {
            if (field != value)
            {
                field = value;
                Raise(HighlightChanged);
            }
        }
    }

    public RgbColor? OverrideColor
    {
        get;
        internal set
        {
            if (field != value)
            {
                var had = HasColor;
                field = value;
                ColorHex = value is { } color
                    ? string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B)
                    : string.Empty;
                Raise(OverrideColorChanged);
                Raise(ColorHexChanged);
                if (had != HasColor)
                {
                    Raise(HasColorChanged);
                }
            }
        }
    }

    public string ColorHex { get; private set; } = string.Empty;

    public bool HasColor => OverrideColor is not null;

    public bool IsHalftone
    {
        get;
        internal set
        {
            if (field != value)
            {
                field = value;
                Raise(HalftoneChanged);
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
