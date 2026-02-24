using Serilog.Sinks.RichTextBoxForms.Tokens;

namespace RevitDevTool.Logging.Serilog;

internal static class RevitTokenSearchService
{
    internal static IList<ElementId> SearchActiveDocument(DetectedToken token)
    {
        var uiDocument = Context.UiApplication.ActiveUIDocument;
        return uiDocument?.Document == null ? [] : SearchDocument(uiDocument.Document, token);
    }

    internal static bool TrySearchAndSelectInActiveDocument(DetectedToken token)
    {
        var uiDocument = Context.UiApplication?.ActiveUIDocument;
        if (uiDocument?.Document == null)
        {
            return false;
        }

        var ids = SearchDocument(uiDocument.Document, token);
        if (ids.Count == 0)
        {
            return false;
        }

        uiDocument.Selection.SetElementIds(ids);
        uiDocument.ShowElements(ids);
        return true;
    }

    private static List<ElementId> SearchDocument(Document document, DetectedToken token)
    {
        return token.Kind switch
        {
            RevitTokenKind.ElementId => SearchByElementId(document, token.NormalizedValue),
            RevitTokenKind.UniqueId => SearchByUniqueId(document, token.NormalizedValue),
            RevitTokenKind.IfcGuid => SearchByIfcGuid(document, token.NormalizedValue),
            _ => []
        };
    }

    private static List<ElementId> SearchByElementId(Document document, string normalizedValue)
    {
#if REVIT2024_OR_GREATER
        if (!long.TryParse(normalizedValue, out var id))
        {
            return [];
        }

        var element = document.GetElement(new ElementId(id));
#else
        if (!int.TryParse(normalizedValue, out var id))
        {
            return [];
        }

        var element = document.GetElement(new ElementId(id));
#endif

        if (element == null)
        {
            return [];
        }

        return [element.Id];
    }

    private static List<ElementId> SearchByUniqueId(Document document, string normalizedValue)
    {
        var element = document.GetElement(normalizedValue);
        if (element == null)
        {
            return [];
        }

        return [element.Id];
    }

    private static List<ElementId> SearchByIfcGuid(Document document, string normalizedValue)
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

        return elementIds.ToList();
    }
}
