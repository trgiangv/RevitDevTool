using RevitDevTool.Core;
using RevitDevTool.Logging.Enums;
using Serilog.Sinks.RichTextBoxForms.Tokens;
namespace RevitDevTool.Logging;

internal static class RevitSearchService
{
    internal static void TrySearchAndSelectInActiveDocument(DetectedToken token)
    {
        var uiDocument = RevitContext.UiApplication.ActiveUIDocument;
        if (uiDocument?.Document == null) return;
        var id = SearchDocument(uiDocument.Document, token);
        if (id == ElementId.InvalidElementId) return;
        var ids = new List<ElementId> { id };
        uiDocument.Selection.SetElementIds(ids);
        uiDocument.ShowElements(ids);
    }

    private static ElementId SearchDocument(Document document, DetectedToken token)
    {
        return token.Kind switch
        {
            nameof(RevitTokenKind.ElementId) => SearchByElementId(document, token.NormalizedValue),
            nameof(RevitTokenKind.UniqueId) => SearchByUniqueId(document, token.NormalizedValue),
            nameof(RevitTokenKind.IfcGuid) => SearchByIfcGuid(document, token.NormalizedValue),
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

    private static ElementId SearchByIfcGuid(Document document, string normalizedValue)
    {
        var guidProvider = new ParameterValueProvider(new ElementId(BuiltInParameter.IFC_GUID));
        var typeGuidProvider = new ParameterValueProvider(new ElementId(BuiltInParameter.IFC_TYPE_GUID));
#if REVIT2022_OR_GREATER
        var guidRule = new FilterStringRule(guidProvider, new FilterStringEquals(), normalizedValue);
        var typeRule = new FilterStringRule(typeGuidProvider, new FilterStringEquals(), normalizedValue);
#else
        var guidRule = new FilterStringRule(guidProvider, new FilterStringEquals(), normalizedValue, true);
        var typeRule = new FilterStringRule(typeGuidProvider, new FilterStringEquals(), normalizedValue, true);
#endif
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

        return elementIds.FirstOrDefault() ?? ElementId.InvalidElementId;
    }
}
