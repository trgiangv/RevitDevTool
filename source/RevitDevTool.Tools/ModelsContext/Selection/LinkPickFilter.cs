using Autodesk.Revit.UI.Selection;
using DevTools.ModelsContext.Documents;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;

namespace RevitDevTool.Tools.ModelsContext.Selection;

internal sealed class LinkPickFilter : ISelectionFilter
{
    private readonly HashSet<long> _categories;
    private readonly Dictionary<long, Document> _links;

    private LinkPickFilter(Dictionary<long, Document> links, HashSet<long> categories)
    {
        _links = links;
        _categories = categories;
    }

    public bool IsEmpty => _links.Count == 0 || _categories.Count == 0;

    public static LinkPickFilter Create(Document model, IEnumerable<DocumentKey> links, IEnumerable<long> categoryIds)
    {
        var modelId = DocumentRegistry.ModelIdOf(model);
        var documents = new Dictionary<long, Document>();
        foreach (var link in links)
        {
            if (!link.IsCurrentModel
                && string.Equals(link.ModelId, modelId, StringComparison.Ordinal)
                && model.GetElement(link.LinkInstanceId.ToElementId()) is RevitLinkInstance instance
                && instance.GetLinkDocument() is { } linkDocument)
            {
                documents[link.LinkInstanceId] = linkDocument;
            }
        }

        return new LinkPickFilter(documents, [.. categoryIds]);
    }

    public bool AllowElement(Element elem)
    {
        return elem is RevitLinkInstance && _links.ContainsKey(elem.Id.ToLong());
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return reference.LinkedElementId != ElementId.InvalidElementId
               && _links.TryGetValue(reference.ElementId.ToLong(), out var linkDocument)
               && linkDocument.GetElement(reference.LinkedElementId)?.Category is { } category
               && _categories.Contains(category.Id.ToLong());
    }
}
