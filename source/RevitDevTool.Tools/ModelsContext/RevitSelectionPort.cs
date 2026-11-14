using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class RevitSelectionPort : ISelectionPort
{
    public Task<IReadOnlyCollection<ElementRef>> GetSelectionAsync(CancellationToken cancellationToken)
    {
        return RevitCall.RunAsync<IReadOnlyCollection<ElementRef>>(application =>
        {
            if (application.ActiveUIDocument is not { } active)
            {
                return [];
            }

            var selection = new HashSet<ElementRef>(ToRefs(active.Document, active.Selection.GetReferences()));
            var model = DocumentRegistry.KeyOf(active.Document);
            selection.UnionWith(active.Selection.GetElementIds().Select(id => new ElementRef(model, RevitIds.ValueOf(id))));
            return selection;
        }, cancellationToken);
    }

    public Task SelectAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken)
    {
        return RevitCall.RunAsync(application =>
        {
            if (application.ActiveUIDocument is not { } active)
            {
                throw new InvalidOperationException(RevitCall.Busy);
            }

            var model = active.Document;
            var modelId = DocumentRegistry.ModelIdOf(model);
            var hostIds = new List<ElementId>();
            var references = new List<Reference>();
            foreach (var group in elements.Where(element => element.Document.ModelId == modelId)
                         .GroupBy(element => element.Document))
            {
                if (group.Key.IsCurrentModel)
                {
                    hostIds.AddRange(group.Select(element => RevitIds.From(element.Id))
                        .Where(id => model.GetElement(id) is not null));
                    continue;
                }

                if (model.GetElement(RevitIds.From(group.Key.LinkInstanceId)) is not RevitLinkInstance instance
                    || instance.GetLinkDocument() is not { } linkDocument)
                {
                    continue;
                }

                foreach (var element in group)
                {
                    if (linkDocument.GetElement(RevitIds.From(element.Id)) is { } linked)
                    {
                        references.Add(new Reference(linked).CreateLinkReference(instance));
                    }
                }
            }

            if (references.Count == 0)
            {
                active.Selection.SetElementIds(hostIds);
                return;
            }

            foreach (var id in hostIds)
            {
                references.Add(new Reference(model.GetElement(id)));
            }

            active.Selection.SetReferences(references);
        }, cancellationToken);
    }

    internal static IEnumerable<ElementRef> ToRefs(Document model, IEnumerable<Reference> references)
    {
        var modelId = DocumentRegistry.ModelIdOf(model);
        foreach (var reference in references)
        {
            if (reference.LinkedElementId != ElementId.InvalidElementId)
            {
                yield return new ElementRef(DocumentKey.Link(modelId, RevitIds.ValueOf(reference.ElementId)),
                    RevitIds.ValueOf(reference.LinkedElementId));
            }
            else if (reference.ElementId != ElementId.InvalidElementId)
            {
                yield return new ElementRef(DocumentKey.Current(modelId), RevitIds.ValueOf(reference.ElementId));
            }
        }
    }
}
