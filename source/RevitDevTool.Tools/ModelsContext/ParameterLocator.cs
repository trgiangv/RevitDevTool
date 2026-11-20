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
    TypePresence Types,
    IReadOnlyCollection<long> Lacking)
{
    public HeldMeaning Held { get; init; } = HeldMeaning.Value;

    public SilentElements Silent { get; init; } = SilentElements.None;

    public bool SilentChecked { get; init; }

    public ElementId ElementId => Id.ToElementId();

    public ForgeTypeId? Spec => SpecTypeId is null ? null : new ForgeTypeId(SpecTypeId);

    public bool IsYesNo => SpecInfo.IsYesNo(Spec);
}

internal enum HeldMeaning
{
    Value,
    Empty,
    Absent
}

internal sealed record SilentElements(IReadOnlyCollection<long> Filled, IReadOnlyCollection<long> Empty)
{
    public static SilentElements None { get; } = new([], []);

    public int Count => Filled.Count + Empty.Count;

    public IReadOnlyCollection<long> All => [.. Filled, .. Empty];
}

internal static class ParameterLocator
{
    private const int HeldSamples = 25;

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
        var located = Locate(document, key, categories, known);
        return WithLacking(document, key, categories, WithTypes(document, key, categories, located));
    }

    public static IReadOnlyList<ResolvedParameter> Locate(
        Document document,
        ParameterKey key,
        ICollection<ElementId> categories,
        IReadOnlyCollection<long>? known = null)
    {
        var located = new List<ResolvedParameter>();
        try
        {
            foreach (var value in IdsOf(document, key, categories, known))
            {
                var parameter = Probe(document, categories, key, value.ToElementId());
                located.Add(new ResolvedParameter(value, key, parameter?.StorageType ?? StorageType.None,
                    parameter is null ? null : SpecInfo.SpecOf(parameter.Definition)?.TypeId, TypePresence.None, []));
            }
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Locate parameter", $"{document.Title} {key.Name}");
        }

        return located;
    }

    public static IReadOnlyList<ResolvedParameter> WithTypes(
        Document document,
        ParameterKey key,
        ICollection<ElementId> categories,
        IReadOnlyList<ResolvedParameter> located)
    {
        var typed = new List<ResolvedParameter>();
        try
        {
            ICollection<ElementId>? types = null;
            foreach (var item in located)
            {
                var id = item.ElementId;
                types ??= TypeIdsOf(document, categories);
                var storage = item.Storage;
                var spec = item.SpecTypeId;
                var presence = storage is StorageType.ElementId or StorageType.String
                    ? PresenceOn(document, types, key, id, storage)
                    : PresenceOn(document, types, key, id, StorageType.None);
                if (storage == StorageType.None)
                {
                    var type = presence.IsEmpty
                        ? null
                        : document.GetElement(presence.TypesFor(FilterOperator.Defined).First().ToElementId());
                    if (type is null || FindOn(type, key, id) is not { } parameter)
                    {
                        continue;
                    }

                    storage = parameter.StorageType;
                    spec = SpecInfo.SpecOf(parameter.Definition)?.TypeId;
                    if (storage is StorageType.ElementId or StorageType.String)
                    {
                        presence = PresenceOn(document, types, key, id, storage);
                    }
                }

                typed.Add(item with
                {
                    Storage = storage,
                    SpecTypeId = spec,
                    Types = presence,
                    Held = HeldOf(document, categories, key, id, storage)
                });
            }
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Parameter types", $"{document.Title} {key.Name}");
        }

        return typed;
    }

    public static IReadOnlyList<ResolvedParameter> WithLacking(
        Document document,
        ParameterKey key,
        ICollection<ElementId> categories,
        IReadOnlyList<ResolvedParameter> typed)
    {
        var resolved = new List<ResolvedParameter>();
        try
        {
            foreach (var item in typed)
            {
                resolved.Add(item with { Lacking = LackingOf(document, categories, key, item.ElementId) });
            }
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Parameter lacking", $"{document.Title} {key.Name}");
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
        if (own is { HasValue: true } && key.BuiltInName is null)
        {
            return own;
        }

        var type = element.Document.GetElement(element.GetTypeId()) is { } owner ? FindOn(owner, key, id) : null;
        if (own is { HasValue: true })
        {
            return type is null || Seen(element, own) ? own : type;
        }

        return type ?? own;
    }

    private static bool Seen(Element element, Parameter parameter)
    {
        return Compiled.Or([RevitRuleFactory.HasValue(parameter.Id), RevitRuleFactory.Holds(parameter.Id, parameter.StorageType)])
            .Passes(element);
    }

    private static Parameter? Probe(Document document, ICollection<ElementId> categories, ParameterKey key, ElementId id)
    {
        Compiled[] probes =
        [
            RevitRuleFactory.HasValue(id),
            RevitRuleFactory.Holds(id, StorageType.Double),
            RevitRuleFactory.Holds(id, StorageType.Integer),
            RevitRuleFactory.HasNoValue(id)
        ];
        foreach (var probe in probes.OfType<Compiled.Filter>())
        {
            var parameter = document.CollectElements()
                .WherePasses(ElementReader.AnyOptionFilter(document, categories))
                .WherePasses(probe.Value)
                .Take(HeldSamples)
                .Select(element => Find(element, key, id))
                .FirstOrDefault(found => found is not null);
            if (parameter is not null)
            {
                return parameter;
            }
        }

        return null;
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

    private static TypePresence PresenceOn(
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
                .Where(type => document.GetElement(type.ToElementId()) is { } element && FindOn(element, key, id) is not null)
                .ToArray());
    }

    private static long[] LackingOf(Document document, ICollection<ElementId> categories, ParameterKey key, ElementId id)
    {
        return document.CollectElements()
            .WherePasses(ElementReader.AnyOptionFilter(document, categories))
            .WherePasses(new ElementParameterFilter(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(id)))
            .Where(element => Find(element, key, id) is null)
            .Select(element => element.Id.ToLong())
            .ToArray();
    }

    private static HeldMeaning HeldOf(
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
            .Take(HeldSamples)
            .Select(element => Find(element, key, id) is { } parameter
                ? parameter.HasValue ? HeldMeaning.Value : HeldMeaning.Empty
                : HeldMeaning.Absent)
            .GroupBy(meaning => meaning)
            .Select(group => (Meaning: group.Key, Count: group.Count()))
            .ToList();
        return votes.Count == 0
            ? HeldMeaning.Value
            : votes.OrderByDescending(vote => vote.Count).ThenBy(vote => vote.Meaning).First().Meaning;
    }

    internal static long[] UnseenIds(Document document, ICollection<ElementId> categories, ResolvedParameter item)
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

        var carrying = (categories.Count > 0 ? categories : ElementReader.ModelCategoryIds(document))
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

    internal static bool Lists(Element element, ParameterKey key, ElementId id)
    {
        if (key.BuiltInName is null || !Enum.TryParse<BuiltInParameter>(key.BuiltInName, out var builtIn))
        {
            return FindOn(element, key, id) is not null;
        }

        if (element.get_Parameter(builtIn) is null)
        {
            return false;
        }

        foreach (Parameter parameter in element.Parameters)
        {
            if (parameter.Definition is InternalDefinition definition && definition.BuiltInParameter == builtIn)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsFilled(Parameter parameter)
    {
        return parameter.HasValue && parameter.StorageType switch
        {
            StorageType.String => !string.IsNullOrEmpty(parameter.AsString()),
            StorageType.ElementId => parameter.AsElementId() != ElementId.InvalidElementId,
            _ => true
        };
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

    internal static Parameter? FindOn(Element element, ParameterKey key, ElementId? id)
    {
        if (key.BuiltInName is { } builtInName)
        {
            return Enum.TryParse<BuiltInParameter>(builtInName, out var builtIn) ? element.get_Parameter(builtIn) : null;
        }

        if (key.SharedId is { } shared)
        {
            return element.get_Parameter(shared);
        }

        Parameter? first = null;
        foreach (var parameter in element.GetParameters(key.Name))
        {
            if (!IsWanted(parameter, key, id))
            {
                continue;
            }

            if (id is not null || IsFilled(parameter))
            {
                return parameter;
            }

            first ??= parameter;
        }

        return first;
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
