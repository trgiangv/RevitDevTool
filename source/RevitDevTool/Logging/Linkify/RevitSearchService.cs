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
        uiDocument.Selection.SetElementIds(elementIds);
        uiDocument.ShowElements(elementIds);
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
