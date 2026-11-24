namespace DevTools.ModelsContext.Documents;

public static class SubcategoryRule
{
    private const string CenterLine = "CenterLine";

    private static readonly HashSet<string> ParentsWithoutRows = new(StringComparer.Ordinal)
    {
        "OST_Site",
        "OST_Lines",
        "OST_Topography"
    };

    public static bool ListsChildrenOf(string parent)
    {
        return !ParentsWithoutRows.Contains(parent);
    }

    public static bool Lists(string parent, string subcategory)
    {
        return ListsChildrenOf(parent) && subcategory.IndexOf(CenterLine, StringComparison.OrdinalIgnoreCase) < 0;
    }
}
