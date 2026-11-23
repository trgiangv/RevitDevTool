namespace DevTools.ModelsContext.Explorer;

public readonly record struct RowPath(int Group, int Family, int Type, long ElementId)
{
    public const int Any = -1;
    public const long Unset = -1;

    public static RowPath None { get; } = new(Any, Any, Any, Unset);

    public static RowPath ForGroup(int group)
    {
        return new RowPath(group, Any, Any, Unset);
    }

    public static RowPath ForFamily(int group, int family)
    {
        return new RowPath(group, family, Any, Unset);
    }

    public static RowPath ForType(int group, int family, int type)
    {
        return new RowPath(group, family, type, Unset);
    }

    public static RowPath ForElement(long elementId)
    {
        return new RowPath(Any, Any, Any, elementId);
    }
}
