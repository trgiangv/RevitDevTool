namespace DevTools.ModelsContext.Model;

public sealed class TreeRow
{
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
    }

    public TreeRowKind Kind { get; }

    public string Text { get; }

    public TreeRow? Parent { get; }

    public DocumentKey? Document { get; }

    public RowPath Path { get; }

    public long Id { get; }

    public int Level { get; }

    public int Count { get; set; }

    public int SelectedInside { get; set; }

    public bool IsExpanded { get; set; }

    public bool HasChildren { get; set; }

    public IReadOnlyList<TreeRow>? Children { get; set; }

    public RowEye Eye { get; set; }

    public string Key { get; init; } = string.Empty;

    public string? DisabledReason { get; set; }

    public bool IsEnabled => DisabledReason is null;

    public int Badge => IsExpanded ? 0 : SelectedInside;

    public bool ChildrenLoaded => Children is not null;

    public bool IsHeader => Kind is TreeRowKind.AllModelsHeader or TreeRowKind.CurrentModelHeader;

    public bool IsInsideLink => Document is { IsCurrentModel: false } && Kind != TreeRowKind.LinkModel;
}
