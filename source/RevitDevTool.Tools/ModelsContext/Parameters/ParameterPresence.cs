using DevTools.ModelsContext.Filters;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Filters;

namespace RevitDevTool.Tools.ModelsContext.Parameters;

internal static class ParameterPresence
{
    public static ICollection<ElementId> TypeIdsOf(Document document, ICollection<ElementId> categories)
    {
        var types = new HashSet<ElementId>(document.CollectElements()
            .WherePasses(new ElementMulticategoryFilter(categories.Count > 0 ? categories : ElementReader.ListedCategoryIds(document)))
            .WhereElementIsElementType()
            .ToElementIds());
        foreach (var element in document.CollectElements().WherePasses(ElementReader.AnyOptionFilter(document, categories)))
        {
            if (element.GetTypeId() is { } typeId && typeId != ElementId.InvalidElementId)
            {
                types.Add(typeId);
            }
        }

        return types;
    }

    public static TypePresence PresenceOn(
        Document document,
        ICollection<ElementId> types,
        ParameterKey key,
        ElementId id,
        StorageType storage)
    {
        if (types.Count == 0)
        {
            return TypePresence.None;
        }

        return new TypePresence(
            TypesPassing(document, types, RevitRuleFactory.HasValue(id, storage)),
            TypesPassing(document, types, RevitRuleFactory.HasNoValue(id, storage))
                .Where(type => document.GetElement(type.ToElementId()) is { } element && ParameterLocator.FindOn(element, key, id) is not null)
                .ToArray());
    }

    public static long[] LackingOf(Document document, ICollection<ElementId> categories, ParameterKey key, ElementId id)
    {
        return document.CollectElements()
            .WherePasses(ElementReader.AnyOptionFilter(document, categories))
            .WherePasses(new ElementParameterFilter(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(id)))
            .Where(element => ParameterLocator.Find(element, key, id) is null)
            .Select(element => element.Id.ToLong())
            .ToArray();
    }

    public static HeldMeaning HeldOf(
        Document document,
        ICollection<ElementId> categories,
        ParameterKey key,
        ElementId id,
        StorageType storage)
    {
        var blind = Compiled.And([
            RevitRuleFactory.Holds(id, storage),
            Compiled.Not(RevitRuleFactory.HasValue(id)),
            Compiled.Not(RevitRuleFactory.HasNoValue(id))
        ]);
        if (blind is not Compiled.Filter filter)
        {
            return HeldMeaning.Value;
        }

        var votes = document.CollectElements()
            .WherePasses(ElementReader.AnyOptionFilter(document, categories))
            .WherePasses(filter.Value)
            .Take(ParameterLocator.HeldSamples)
            .Select(element => MeaningOn(element, key, id))
            .GroupBy(meaning => meaning)
            .Select(group => (Meaning: group.Key, Count: group.Count()))
            .ToList();
        return votes.Count == 0
            ? HeldMeaning.Value
            : votes.OrderByDescending(vote => vote.Count).ThenBy(vote => vote.Meaning).First().Meaning;
    }

    public static long[] SilentIds(Document document, ICollection<ElementId> categories, ResolvedParameter item)
    {
        var id = item.ElementId;
        var holds = RevitRuleFactory.Holds(id, item.Storage);
        var seen = Compiled.Or([RevitRuleFactory.HasValue(id), RevitRuleFactory.HasNoValue(id), holds]);
        if (item.Storage == StorageType.None
            || (!item.Types.IsEmpty && holds is Compiled.MatchNone)
            || seen is not Compiled.Filter seenFilter
            || Compiled.Not(seen) is not Compiled.Filter unseen)
        {
            return [];
        }

        var carrying = (categories.Count > 0 ? categories : ElementReader.UsedCategoryIds(document))
            .Where(category => document.CollectElements()
                .WherePasses(ElementReader.AnyOptionFilter(document, [category]))
                .WherePasses(seenFilter.Value)
                .FirstElementId() != ElementId.InvalidElementId)
            .ToList();
        return carrying.Count == 0
            ? []
            : document.CollectElements()
                .WherePasses(ElementReader.AnyOptionFilter(document, carrying))
                .WherePasses(unseen.Value)
                .ToElementIds()
                .Select(element => element.ToLong())
                .ToArray();
    }

    private static HeldMeaning MeaningOn(Element element, ParameterKey key, ElementId id)
    {
        if (ParameterLocator.Find(element, key, id) is not { } parameter)
        {
            return HeldMeaning.Absent;
        }

        return parameter.HasValue ? HeldMeaning.Value : HeldMeaning.Empty;
    }

    private static long[] TypesPassing(Document document, ICollection<ElementId> types, Compiled rule)
    {
        return rule is Compiled.Filter filter
            ? document.CollectElements(types)
                .WherePasses(filter.Value)
                .ToElementIds()
                .Select(type => type.ToLong())
                .ToArray()
            : [];
    }
}
