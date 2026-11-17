using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using Nice3point.Revit.Extensions;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed record ResolvedParameter(
    long Id,
    ParameterKey Key,
    StorageType Storage,
    string? SpecTypeId,
    TypePresence Types)
{
    public ElementId ElementId => Id.ToElementId();

    public ForgeTypeId? Spec => SpecTypeId is null ? null : new ForgeTypeId(SpecTypeId);

    public bool IsYesNo => SpecInfo.IsYesNo(Spec);
}

internal static class ParameterLocator
{
    public static ParameterKey KeyOf(Document document, ElementId parameterId)
    {
        try
        {
            var value = parameterId.ToLong();
            if (value < 0)
            {
                var builtIn = (BuiltInParameter)(int)value;
                return ParameterKey.BuiltIn(builtIn.ToLabel(), builtIn.ToString());
            }

            if (document.GetElement(parameterId) is ParameterElement element)
            {
                var definition = element.GetDefinition();
                return element is SharedParameterElement shared
                    ? ParameterKey.Shared(definition.Name, shared.GuidValue)
                    : ParameterKey.Named(definition.Name, SpecInfo.DataTypeOf(SpecInfo.SpecOf(definition), null));
            }
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Parameter key", parameterId);
        }

        return new ParameterKey(parameterId.ToString());
    }

    public static IReadOnlyCollection<long> IdsOf(
        Document document,
        ParameterKey key,
        ICollection<ElementId> categories,
        IReadOnlyCollection<long>? known)
    {
        if (key.BuiltInName is { } builtInName)
        {
            return Enum.TryParse<BuiltInParameter>(builtInName, out var builtIn) && builtIn != BuiltInParameter.INVALID
                ? [builtIn.ToLong()]
                : [];
        }

        if (key.SharedId is { } shared)
        {
            return SharedParameterElement.Lookup(document, shared) is { } element ? [element.Id.ToLong()] : [];
        }

        return known ?? NamedIds(document, key, categories);
    }

    public static IReadOnlyList<ResolvedParameter> Resolve(
        Document document,
        ParameterKey key,
        ICollection<ElementId> categories,
        IReadOnlyCollection<long>? known = null)
    {
        var resolved = new List<ResolvedParameter>();
        try
        {
            ICollection<ElementId>? types = null;
            foreach (var value in IdsOf(document, key, categories, known))
            {
                var id = value.ToElementId();
                types ??= TypeIdsOf(document, categories);
                var presence = PresenceOn(document, types, id, StorageType.None);
                var element = document.CollectElements()
                    .WherePasses(ElementReader.AnyOptionFilter(document, categories))
                    .WherePasses(Holds(id))
                    .FirstElement();
                var parameter = element is not null ? Find(element, key, id) : null;
                if (parameter is null && !presence.IsEmpty)
                {
                    var type = document.GetElement(presence.TypesFor(FilterOperator.Defined).First().ToElementId());
                    parameter = type is null ? null : FindOn(type, key, id);
                }

                if (parameter is not null)
                {
                    if (parameter.StorageType is StorageType.ElementId or StorageType.String)
                    {
                        presence = PresenceOn(document, types, id, parameter.StorageType);
                    }

                    resolved.Add(new ResolvedParameter(value, key, parameter.StorageType,
                        SpecInfo.SpecOf(parameter.Definition)?.TypeId, presence));
                }
            }
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Resolve parameter", $"{document.Title} {key.Name}");
        }

        return resolved;
    }

    public static bool Matches(ParameterKey key, Parameter parameter)
    {
        return key.BuiltInName is null && key.SharedId is null && !parameter.IsShared
               && parameter.Id.ToLong() > 0
               && string.Equals(parameter.Definition?.Name, key.Name, StringComparison.Ordinal)
               && SpecInfo.Fits(key, SpecInfo.SpecOf(parameter.Definition), parameter.StorageType);
    }

    public static Parameter? Find(Element element, ParameterKey key, ElementId? id = null)
    {
        var own = FindOn(element, key, id);
        if (own is { HasValue: true })
        {
            return own;
        }

        return (element.Document.GetElement(element.GetTypeId()) is { } type ? FindOn(type, key, id) : null) ?? own;
    }

    private static ElementFilter Holds(ElementId id)
    {
        return new LogicalOrFilter(
            new ElementParameterFilter(ParameterFilterRuleFactory.CreateHasValueParameterRule(id)),
            new ElementParameterFilter(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(id)));
    }

    private static ICollection<ElementId> TypeIdsOf(Document document, ICollection<ElementId> categories)
    {
        var types = new HashSet<ElementId>(document.CollectElements()
            .WherePasses(new ElementMulticategoryFilter(categories.Count > 0 ? categories : ElementReader.ModelCategoryIds(document)))
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

    private static TypePresence PresenceOn(Document document, ICollection<ElementId> types, ElementId id, StorageType storage)
    {
        if (types.Count == 0)
        {
            return TypePresence.None;
        }

        return new TypePresence(
            TypesPassing(document, types, RevitRuleFactory.HasValue(id, storage)),
            TypesPassing(document, types, RevitRuleFactory.HasNoValue(id, storage)));
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

    private static HashSet<long> NamedIds(Document document, ParameterKey key, ICollection<ElementId> categories)
    {
        var ids = new HashSet<long>(document.CollectElements()
            .OfClass<ParameterElement>()
            .Cast<ParameterElement>()
            .Where(element => element is not SharedParameterElement
                              && element.GetDefinition() is { } definition
                              && string.Equals(definition.Name, key.Name, StringComparison.Ordinal)
                              && SpecInfo.Fits(key, SpecInfo.SpecOf(definition), null))
            .Select(element => element.Id.ToLong()));
        var sampled = new HashSet<long>();
        foreach (var element in document.CollectElements().WherePasses(ElementReader.AnyOptionFilter(document, categories)))
        {
            var typeId = element.GetTypeId();
            if (!sampled.Add(typeId != ElementId.InvalidElementId ? typeId.ToLong() : element.Category?.Id.ToLong() ?? 0))
            {
                continue;
            }

            AddNamed(ids, element, key);
            if (document.GetElement(typeId) is { } type)
            {
                AddNamed(ids, type, key);
            }
        }

        return ids;
    }

    private static void AddNamed(HashSet<long> ids, Element element, ParameterKey key)
    {
        foreach (var parameter in element.GetParameters(key.Name))
        {
            if (Matches(key, parameter))
            {
                ids.Add(parameter.Id.ToLong());
            }
        }
    }

    private static Parameter? FindOn(Element element, ParameterKey key, ElementId? id)
    {
        if (key.BuiltInName is { } builtInName)
        {
            return Enum.TryParse<BuiltInParameter>(builtInName, out var builtIn) ? element.get_Parameter(builtIn) : null;
        }

        if (key.SharedId is { } shared)
        {
            return element.get_Parameter(shared);
        }

        foreach (var parameter in element.GetParameters(key.Name))
        {
            if (IsWanted(parameter, key, id))
            {
                return parameter;
            }
        }

        return null;
    }

    private static bool IsWanted(Parameter parameter, ParameterKey key, ElementId? id)
    {
        if (id is not null)
        {
            return parameter.Id == id;
        }

        return !parameter.IsShared && SpecInfo.Fits(key, SpecInfo.SpecOf(parameter.Definition), parameter.StorageType);
    }
}
