using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Filters;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Filters;
using RevitDevTool.Tools.ModelsContext.Pane;

namespace RevitDevTool.Tools.ModelsContext.Parameters;

internal static class ParameterLocator
{
    internal const int HeldSamples = 25;

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
                types ??= ParameterPresence.TypeIdsOf(document, categories);
                if (TypedOf(document, categories, key, types, item) is { } result)
                {
                    typed.Add(result);
                }
            }
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Parameter types", $"{document.Title} {key.Name}");
        }

        return typed;
    }

    private static ResolvedParameter? TypedOf(
        Document document,
        ICollection<ElementId> categories,
        ParameterKey key,
        ICollection<ElementId> types,
        ResolvedParameter item)
    {
        var id = item.ElementId;
        var storage = item.Storage;
        var spec = item.SpecTypeId;
        var presence = PresenceFor(document, types, key, id, storage);
        if (storage == StorageType.None)
        {
            if (FoundOnFirstType(document, key, id, presence) is not { } parameter)
            {
                return null;
            }

            storage = parameter.StorageType;
            spec = SpecInfo.SpecOf(parameter.Definition)?.TypeId;
            if (UsesStorageRules(storage))
            {
                presence = PresenceFor(document, types, key, id, storage);
            }
        }

        return item with
        {
            Storage = storage,
            SpecTypeId = spec,
            Types = presence,
            Held = ParameterPresence.HeldOf(document, categories, key, id, storage)
        };
    }

    private static bool UsesStorageRules(StorageType storage)
    {
        return storage is StorageType.ElementId or StorageType.String;
    }

    private static TypePresence PresenceFor(
        Document document,
        ICollection<ElementId> types,
        ParameterKey key,
        ElementId id,
        StorageType storage)
    {
        return ParameterPresence.PresenceOn(document, types, key, id, UsesStorageRules(storage) ? storage : StorageType.None);
    }

    private static Parameter? FoundOnFirstType(Document document, ParameterKey key, ElementId id, TypePresence presence)
    {
        if (presence.IsEmpty)
        {
            return null;
        }

        var type = document.GetElement(presence.TypesFor(FilterOperator.Defined).First().ToElementId());
        return type is null ? null : FindOn(type, key, id);
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
                resolved.Add(item with { Lacking = ParameterPresence.LackingOf(document, categories, key, item.ElementId) });
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

    private static HashSet<long> NamedIds(Document document, ParameterKey key, ICollection<ElementId> categories)
    {
        var ids = new HashSet<long>(DeclaredIds(document, key));
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

    private static IEnumerable<long> DeclaredIds(Document document, ParameterKey key)
    {
        return document.CollectElements()
            .OfClass<ParameterElement>()
            .Cast<ParameterElement>()
            .Where(element => element is not SharedParameterElement
                              && element.GetDefinition() is { } definition
                              && string.Equals(definition.Name, key.Name, StringComparison.Ordinal)
                              && SpecInfo.Fits(key, SpecInfo.SpecOf(definition), null))
            .Select(element => element.Id.ToLong());
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

        return FindNamed(element, key, id);
    }

    private static Parameter? FindNamed(Element element, ParameterKey key, ElementId? id)
    {
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
