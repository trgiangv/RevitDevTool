using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using Nice3point.Revit.Extensions;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class RevitSelectionPort : ISelectionPort
{
    public Task<IReadOnlyCollection<ElementRef>> GetSelectionAsync(CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync<IReadOnlyCollection<ElementRef>>("Read selection", application =>
        {
            if (application.ActiveUIDocument is not { } active)
            {
                return [];
            }

            return ToSelection(active.Document, active.Selection.GetReferences(), active.Selection.GetElementIds());
        }, cancellationToken);
    }

    internal static HashSet<ElementRef> ToSelection(Document model, IList<Reference> references, ICollection<ElementId> ids)
    {
        var linkHosts = new HashSet<ElementId>(references
            .Where(reference => reference.LinkedElementId != ElementId.InvalidElementId)
            .Select(reference => reference.ElementId));
        var selection = new HashSet<ElementRef>(ToRefs(model, references));
        var key = DocumentRegistry.KeyOf(model);
        selection.UnionWith(ids.Where(id => !linkHosts.Contains(id)).Select(id => new ElementRef(key, id.ToLong())));
        return selection;
    }

    public Task SelectAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Select", application => Select(application, elements), cancellationToken);
    }

    internal static IEnumerable<ElementRef> ToRefs(Document model, IEnumerable<Reference> references)
    {
        var modelId = DocumentRegistry.ModelIdOf(model);
        foreach (var reference in references)
        {
            if (reference.LinkedElementId != ElementId.InvalidElementId)
            {
                yield return new ElementRef(DocumentKey.Link(modelId, reference.ElementId.ToLong()),
                    reference.LinkedElementId.ToLong());
            }
            else if (reference.ElementId != ElementId.InvalidElementId)
            {
                yield return new ElementRef(DocumentKey.Current(modelId), reference.ElementId.ToLong());
            }
        }
    }

    private static void Select(UIApplication application, IReadOnlyCollection<ElementRef> elements)
    {
        if (application.ActiveUIDocument is not { } active)
        {
            throw new InvalidOperationException(RevitErrors.Busy);
        }

        var model = active.Document;
        var hostIds = new List<ElementId>();
        var references = new List<Reference>();
        foreach (var group in DocumentRegistry.GroupByDocument(model, elements))
        {
            if (group.Key.IsCurrentModel)
            {
                hostIds.AddRange(HostIds(model, group));
            }
            else
            {
                references.AddRange(LinkReferences(model, group));
            }
        }

        if (references.Count == 0)
        {
            active.Selection.SetElementIds(hostIds);
            return;
        }

        references.AddRange(hostIds.Select(id => new Reference(model.GetElement(id))));
        active.Selection.SetReferences(references);
    }

    private static IEnumerable<ElementId> HostIds(Document model, IEnumerable<ElementRef> elements)
    {
        return elements.Select(element => element.Id.ToElementId())
            .Where(id => model.GetElement(id) is not null);
    }

    private static IEnumerable<Reference> LinkReferences(Document model, IGrouping<DocumentKey, ElementRef> group)
    {
        if (model.GetElement(group.Key.LinkInstanceId.ToElementId()) is not RevitLinkInstance instance
            || instance.GetLinkDocument() is not { } linkDocument)
        {
            return [];
        }

        return group.Select(element => linkDocument.GetElement(element.Id.ToElementId()))
            .Where(linked => linked is not null)
            .Select(linked => new Reference(linked).CreateLinkReference(instance));
    }
}
