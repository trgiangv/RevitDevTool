using Autodesk.Revit.UI.Selection;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Selection;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Pane;
using RevitDevTool.Tools.ModelsContext.Visibility;

namespace RevitDevTool.Tools.ModelsContext.Selection;

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

    public Task<PickResult> PickAsync(
        IReadOnlyCollection<DocumentKey> links,
        IReadOnlyCollection<long> categoryIds,
        CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Pick", application => Pick(application, links, categoryIds), cancellationToken);
    }

    private static PickResult Pick(UIApplication application, IReadOnlyCollection<DocumentKey> links,
        IReadOnlyCollection<long> categoryIds)
    {
        try
        {
            if (application.ActiveUIDocument is not { } active)
            {
                return PickResult.Cancel;
            }

            var model = active.Document;
            var filter = LinkPickFilter.Create(model, links, categoryIds);
            if (filter.IsEmpty)
            {
                return PickResult.Cancel;
            }

            var picked = active.Selection.PickObjects(ObjectType.LinkedElement, filter, PickController.PickingCue);
            if (!model.IsValidObject || application.ActiveUIDocument?.Document is not { } after || !after.Equals(model))
            {
                return PickResult.Cancel;
            }

            if (picked.Count == 0)
            {
                return PickResult.Of([]);
            }

            var result = PickResult.Of(ToRefs(model, picked));
            Select(application, result.Picked);
            return result;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return PickResult.Cancel;
        }
        catch (OperationCanceledException)
        {
            return PickResult.Cancel;
        }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException)
        {
            return PickResult.Cancel;
        }
        catch (InvalidOperationException)
        {
            return PickResult.Cancel;
        }
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
                references.AddRange(LinkElements.LoadedReferencesOf(model, group.Key, group));
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
}
