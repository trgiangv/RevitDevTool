using RevitDevTool.Logging.Enums;

namespace RevitDevTool.Logging.Linkify;

public static class ElementSearcher
{
    public static SearchMatch? TrySearch(
        Document document,
        RevitTokenKind kind,
        string value,
        string? linkInstanceId)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (linkInstanceId is not null)
            return SearchInLink(document, linkInstanceId, kind, value);

        var ids = FindInDocument(document, kind, value);
        return ids is { Count: > 0 }
            ? new SearchMatch.HostElements(ids)
            : null;
    }

    private static SearchMatch? SearchInLink(
        Document host,
        string linkInstanceIdText,
        RevitTokenKind kind,
        string value)
    {
        if (!LinkToken.TryParseElementId(linkInstanceIdText, out var instanceId))
            return null;
        if (host.GetElement(instanceId) is not RevitLinkInstance instance)
            return null;

        var linkDoc = instance.GetLinkDocument();
        if (linkDoc is null)
            return null;

        var ids = FindInDocument(linkDoc, kind, value);
        return ids is { Count: > 0 }
            ? new SearchMatch.LinkedElements(instance, ids)
            : null;
    }

    private static ICollection<ElementId>? FindInDocument(Document document, RevitTokenKind kind, string value)
    {
        return kind switch
        {
            RevitTokenKind.ElementId => FindByElementId(document, value),
            RevitTokenKind.UniqueId => FindByUniqueId(document, value),
            RevitTokenKind.IfcGuid => FindByIfcGuid(document, value),
            _ => null
        };
    }

    private static ICollection<ElementId>? FindByElementId(Document document, string normalizedValue)
    {
        if (!LinkToken.TryParseElementId(normalizedValue, out var id))
            return null;
        return document.GetElement(id) is null ? null : [id];
    }

    private static ICollection<ElementId>? FindByUniqueId(Document document, string normalizedValue)
    {
        var element = document.GetElement(normalizedValue);
        return element is null ? null : [element.Id];
    }

    private static ICollection<ElementId>? FindByIfcGuid(Document document, string normalizedValue)
    {
        var ids = CollectIfcGuidIds(document, normalizedValue);
        return ids.Count == 0 ? null : ids;
    }

    private static ICollection<ElementId> CollectIfcGuidIds(Document document, string normalizedValue)
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
            elementIds.Add(typeId);

        return elementIds;
    }
}
