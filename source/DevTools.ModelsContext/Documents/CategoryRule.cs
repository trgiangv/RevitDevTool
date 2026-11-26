namespace DevTools.ModelsContext.Documents;

public enum CategoryKind
{
    Model,
    Annotation,
    Other
}

public static class CategoryRule
{
    public const string DetailGroups = "OST_IOSDetailGroups";

    private const string NoBuiltIn = "INVALID";

    private static readonly HashSet<string> Excluded = new(StringComparer.Ordinal)
    {
        "OST_WeakDims",
        "OST_SectionBox",
        "OST_RvtLinks",
        "OST_Materials"
    };

    public static bool Lists(CategoryKind kind, bool visibleInUi, string builtIn, bool inLink = false)
    {
        if (inLink)
        {
            return kind == CategoryKind.Model && visibleInUi && !Excluded.Contains(builtIn);
        }

        if (builtIn == DetailGroups)
        {
            return true;
        }

        return kind is CategoryKind.Model or CategoryKind.Annotation && visibleInUi && !Excluded.Contains(builtIn);
    }

    public static bool ListsSubcategory(
        CategoryKind kind,
        bool visibleInUi,
        string parent,
        string builtIn,
        bool inLink = false)
    {
        return builtIn != NoBuiltIn && Lists(kind, visibleInUi, builtIn, inLink) && SubcategoryRule.Lists(parent, builtIn);
    }
}
