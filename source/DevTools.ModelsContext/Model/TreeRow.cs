using System.ComponentModel;
using System.Globalization;

namespace DevTools.ModelsContext.Model;

public sealed class TreeRow : INotifyPropertyChanged
{
    public const double IndentPerLevel = 14;

    private static readonly PropertyChangedEventArgs HighlightChanged = new(nameof(IsHighlighted));

    private int _count;
    private string _countText = "0";
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
            }
        }
    }

    public string CountText => _countText;

    public int SelectedInside { get; set; }

    public bool IsExpanded { get; set; }

    public bool HasChildren { get; set; }

    public IReadOnlyList<TreeRow>? Children { get; set; }

    public RowEye Eye { get; set; }

    public string Key { get; init; } = string.Empty;

    public string? DisabledReason { get; set; }

    public bool IsEnabled => DisabledReason is null;

    public bool HasCount => Kind is not (TreeRowKind.Instance or TreeRowKind.MorePage);

    public int Badge => IsExpanded ? 0 : SelectedInside;

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
                PropertyChanged?.Invoke(this, HighlightChanged);
            }
        }
    }
}
