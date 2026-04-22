using RevitDevTool.Core;
using RevitDevTool.Logging.Enums;
namespace RevitDevTool.Logging.Linkify;

internal static class RevitSearchService
{
    internal static void TrySearchAndSelectInActiveDocument(RevitTokenKind kind, string value)
    {
        var uiDocument = RevitContext.UiApplication.ActiveUIDocument;
        if (uiDocument?.Document == null) return;
        var result = SearchDocument(uiDocument.Document, kind, value);
        switch (result)
        {
            case ElementId revitId when revitId == ElementId.InvalidElementId:
                return;
            case ElementId revitId:
                ShowElements(uiDocument, [revitId]);
                break;
            case ICollection<ElementId> elementIds:
                ShowElements(uiDocument, elementIds);
                break;
        }
    }
    
    private static void ShowElements(UIDocument uiDocument, ICollection<ElementId> elementIds)
    {
        if (elementIds.Count == 0) return;

        var document = uiDocument.Document;
        var first = document.GetElement(elementIds.First());
        switch (first)
        {
            case null:
                return;
            case Autodesk.Revit.DB.View view:
                uiDocument.RequestViewChange(view);
                return;
        }

        if (first.OwnerViewId != ElementId.InvalidElementId && HasSameOwnerView(document, elementIds))
        {
            var ownerView = (Autodesk.Revit.DB.View)document.GetElement(first.OwnerViewId);
            uiDocument.RequestViewChange(ownerView);
        }

        uiDocument.Selection.SetElementIds(elementIds);
        uiDocument.ShowElements(ResolveVisibleIds(document, elementIds));
    }

    private static bool HasSameOwnerView(Document document, ICollection<ElementId> elementIds)
    {
        ElementId? ownerViewId = null;
        foreach (var id in elementIds)
        {
            var viewId = document.GetElement(id)?.OwnerViewId ?? ElementId.InvalidElementId;
            if (viewId == ElementId.InvalidElementId) return false;
            ownerViewId ??= viewId;
            if (viewId != ownerViewId) return false;
        }
        return true;
    }

    private static ICollection<ElementId> ResolveVisibleIds(Document document, ICollection<ElementId> elementIds)
    {
        List<ElementId>? expanded = null;
        foreach (var id in elementIds)
        {
            if (document.GetElement(id) is not IndependentTag tag) continue;

            expanded ??= new List<ElementId>(elementIds);
            foreach (var hostId in tag.GetTaggedLocalElementIds())
            {
                if (hostId != ElementId.InvalidElementId)
                    expanded.Add(hostId);
            }
        }
        return expanded ?? elementIds;
    }

    private static object SearchDocument(Document document, RevitTokenKind kind, string value)
    {
        return kind switch
        {
            RevitTokenKind.ElementId => SearchByElementId(document, value),
            RevitTokenKind.UniqueId => SearchByUniqueId(document, value),
            RevitTokenKind.IfcGuid => SearchByIfcGuid(document, value),
            _ => ElementId.InvalidElementId
        };
    }

    private static ElementId SearchByElementId(Document document, string normalizedValue)
    {
#if REVIT2024_OR_GREATER
        if (!long.TryParse(normalizedValue, out var id))
        {
            return ElementId.InvalidElementId;
        }
#else
        if (!int.TryParse(normalizedValue, out var id))
        {
            return ElementId.InvalidElementId;
        }
#endif
        var element = document.GetElement(new ElementId(id));
        return element == null ? ElementId.InvalidElementId : element.Id;
    }

    private static ElementId SearchByUniqueId(Document document, string normalizedValue)
    {
        var element = document.GetElement(normalizedValue);
        return element == null ? ElementId.InvalidElementId : element.Id;
    }

    private static ICollection<ElementId> SearchByIfcGuid(Document document, string normalizedValue)
    {
        var guidProvider = new ParameterValueProvider(new ElementId(BuiltInParameter.IFC_GUID));
        var typeGuidProvider = new ParameterValueProvider(new ElementId(BuiltInParameter.IFC_TYPE_GUID));

        var guidRule = new FilterStringRule(guidProvider, new FilterStringEquals(), normalizedValue);
        var typeRule = new FilterStringRule(typeGuidProvider, new FilterStringEquals(), normalizedValue);

        var guidFilter = new ElementParameterFilter(guidRule);
        var typeGuidFilter = new ElementParameterFilter(typeRule);

        var collector = new FilteredElementCollector(document);
        var typeCollector = new FilteredElementCollector(document);

        var elementIds = collector.WherePasses(guidFilter).ToElementIds();
        var typeIds = typeCollector.WherePasses(typeGuidFilter).ToElementIds();
        foreach (var typeId in typeIds)
        {
            elementIds.Add(typeId);
        }

        return elementIds;
    }
}
