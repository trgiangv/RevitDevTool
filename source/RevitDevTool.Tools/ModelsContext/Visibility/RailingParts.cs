namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal static class RailingParts
{
    private static readonly BuiltInCategory[] Parts =
    [
        BuiltInCategory.OST_StairsRailingBaluster, BuiltInCategory.OST_RailingSupport, BuiltInCategory.OST_RailingTermination
    ];

    private static readonly List<BuiltInCategory> Railings =
    [
        BuiltInCategory.OST_StairsRailing, BuiltInCategory.OST_RailingHandRail, BuiltInCategory.OST_RailingTopRail
    ];

    public static Element? RailingOf(Element element)
    {
        if (element.Category is not { } category || !Parts.Contains(category.BuiltInCategory))
        {
            return null;
        }

        var part = new ElementIdSetFilter([element.Id]);
        return new FilteredElementCollector(element.Document)
            .WherePasses(new ElementMulticategoryFilter(Railings))
            .WhereElementIsNotElementType()
            .FirstOrDefault(railing => railing.GetDependentElements(part).Count > 0);
    }
}
