using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Visibility;
using Nice3point.Revit.Extensions;

namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal static class LinkIsolation
{
    public static List<ElementRef> RestOf(Document model, View view, IEnumerable<LinkIsolateStep> steps)
    {
        var rest = new List<ElementRef>();
        foreach (var step in steps)
        {
            var (instance, linkDocument) = LinkElements.RequireLink(model, step.Document);
            var collector = LinkElements.InView(model, view, instance.Id)
                            ?? throw new InvalidOperationException(VisibilityText.NoLinkView);
            var keep = KeptOf(linkDocument, step.KeptIds);
            foreach (var id in collector.WhereElementIsNotElementType().WherePasses(new ElementClassFilter(typeof(Group), true)).ToElementIds())
            {
                if (!keep.Contains(id))
                {
                    rest.Add(new ElementRef(step.Document, id.ToLong()));
                }
            }

            if (rest.Count > LinkRules.MaxIsolateRest)
            {
                throw new InvalidOperationException(VisibilityText.LinkIsolateTooMany);
            }
        }

        return rest;
    }

    private static HashSet<ElementId> KeptOf(Document link, IEnumerable<long> ids)
    {
        var targets = new List<ElementId>();
        NestedComponents.AddWithNested(link, ids.Select(id => id.ToElementId()), targets, []);
        var keep = new HashSet<ElementId>(targets);
        foreach (var id in targets)
        {
            var parent = (link.GetElement(id) as FamilyInstance)?.SuperComponent;
            while (parent is not null && keep.Add(parent.Id))
            {
                parent = (parent as FamilyInstance)?.SuperComponent;
            }
        }

        return keep;
    }
}
