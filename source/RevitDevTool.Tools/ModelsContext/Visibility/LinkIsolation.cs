using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Visibility;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;

namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal static class LinkIsolation
{
    public const string NoLinkView = "This view cannot show linked elements.";

    public static List<ElementRef> RestOf(Document model, View view, IEnumerable<LinkIsolateStep> steps)
    {
        var rest = new List<ElementRef>();
        foreach (var step in steps)
        {
            var (instance, linkDocument) = LinkElements.RequireLink(model, step.Document);
            var collector = LinkElements.InView(model, view, instance.Id)
                            ?? throw new InvalidOperationException(NoLinkView);
            var keep = KeptOf(linkDocument, step.KeepIds);
            foreach (var id in collector.WherePasses(ElementReader.AnyOptionFilter(linkDocument)).ToElementIds())
            {
                if (!keep.Contains(id))
                {
                    rest.Add(new ElementRef(step.Document, id.ToLong()));
                }
            }

            if (rest.Count > VisibilityReasons.LinkIsolateCap)
            {
                throw new InvalidOperationException(VisibilityReasons.LinkIsolateTooMany);
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
