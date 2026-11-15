using DevTools.ModelsContext.Model;
using Nice3point.Revit.Extensions;

namespace RevitDevTool.Tools.ModelsContext;

internal static class LinkElements
{
    public static List<Reference> ReferencesOf(Document model, IEnumerable<ElementRef> elements)
    {
        var references = new List<Reference>();
        foreach (var group in elements.GroupBy(element => element.Document))
        {
            var (instance, linkDocument) = RequireLink(model, group.Key);
            references.AddRange(group.Select(element => linkDocument.GetElement(element.Id.ToElementId()))
                .Where(linked => linked is not null)
                .Select(linked => new Reference(linked).CreateLinkReference(instance)));
        }

        return references;
    }

    public static List<ElementRef> ResolvedOf(Document model, IEnumerable<ElementRef> elements)
    {
        var resolved = new List<ElementRef>();
        foreach (var group in elements.GroupBy(element => element.Document))
        {
            var (_, linkDocument) = RequireLink(model, group.Key);
            resolved.AddRange(group.Where(element => linkDocument.GetElement(element.Id.ToElementId()) is not null));
        }

        return resolved;
    }

    public static HashSet<ElementRef> VisibleOf(Document model, View view, IEnumerable<ElementRef> elements)
    {
        var visible = new HashSet<ElementRef>();
        foreach (var group in elements.GroupBy(element => element.Document))
        {
            var (instance, _) = RequireLink(model, group.Key);
            if (InView(model, view, instance.Id) is not { } collector)
            {
                continue;
            }

            var inView = new HashSet<long>(collector
                .WhereElementIsNotElementType()
                .ToElementIds()
                .Select(id => id.ToLong()));
            visible.UnionWith(group.Where(element => inView.Contains(element.Id)));
        }

        return visible;
    }

    public static FilteredElementCollector? InView(Document model, View view, ElementId linkInstanceId)
    {
        return ShowsLinks(view) ? new FilteredElementCollector(model, view.Id, linkInstanceId) : null;
    }

    private static bool ShowsLinks(View view)
    {
        return !view.IsTemplate && view.ViewType is ViewType.FloorPlan or ViewType.CeilingPlan or ViewType.EngineeringPlan
            or ViewType.AreaPlan or ViewType.Section or ViewType.Elevation or ViewType.Detail or ViewType.ThreeD
            or ViewType.Walkthrough;
    }

    private static (RevitLinkInstance Instance, Document LinkDocument) RequireLink(Document model, DocumentKey link)
    {
        if (!string.Equals(link.ModelId, DocumentRegistry.ModelIdOf(model), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(RevitViewActions.ModelChanged);
        }

        if (model.GetElement(link.LinkInstanceId.ToElementId()) is not RevitLinkInstance instance
            || instance.GetLinkDocument() is not { } linkDocument)
        {
            throw new InvalidOperationException(VisibilityReasons.NotLoaded);
        }

        return (instance, linkDocument);
    }
}
