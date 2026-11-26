using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;

namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal static class TagHosts
{
    public static List<ElementId> WithHosts(Document model, List<ElementId> ids)
    {
        var seen = new HashSet<ElementId>(ids);
        var result = new List<ElementId>(ids);
        foreach (var id in ids)
        {
            foreach (var host in HostsOf(model.GetElement(id)))
            {
                if (seen.Add(host))
                {
                    result.Add(host);
                }
            }
        }

        return result;
    }

    public static HashSet<ElementId> HostCategoriesIn(Document model, View view, ICollection<ElementId> categories)
    {
        var hosts = new HashSet<ElementId>();
        if (categories.Count == 0)
        {
            return hosts;
        }

        var tags = new FilteredElementCollector(model)
            .WherePasses(new ElementOwnerViewFilter(view.Id))
            .WherePasses(new ElementMulticategoryFilter(categories))
            .WhereElementIsNotElementType()
            .Where(element => element is IndependentTag or SpatialElementTag);
        foreach (var tag in tags)
        {
            foreach (var host in HostsOf(tag))
            {
                if (model.GetElement(host)?.Category is { } category)
                {
                    hosts.Add(category.Id);
                }
            }
        }

        return hosts;
    }

    private static IEnumerable<ElementId> HostsOf(Element? element)
    {
        return element switch
        {
            IndependentTag tag => tag.GetTaggedLocalElementIds(),
            RoomTag { TaggedLocalRoomId: var room } when room != ElementId.InvalidElementId => [room],
            SpaceTag { Space: { } space } => [space.Id],
            AreaTag { Area: { } area } => [area.Id],
            _ => []
        };
    }
}
