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
            var inView = new HashSet<long>(new FilteredElementCollector(model, view.Id, instance.Id)
                .WhereElementIsNotElementType()
                .ToElementIds()
                .Select(id => id.ToLong()));
            visible.UnionWith(group.Where(element => inView.Contains(element.Id)));
        }

        return visible;
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
