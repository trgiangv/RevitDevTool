using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Pane;

namespace RevitDevTool.Tools.ModelsContext.Parameters;

internal sealed class CatalogInfoFactory
{
    private readonly Dictionary<string, string?> _disciplines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _groups = new(StringComparer.Ordinal);

    public static Dictionary<long, ParameterBinding> BindingsOf(Document document, FailureTally? failures)
    {
        var bindings = new Dictionary<long, ParameterBinding>();
        try
        {
            var iterator = document.ParameterBindings.ForwardIterator();
            while (iterator.MoveNext())
            {
                if (iterator.Key is InternalDefinition definition)
                {
                    bindings[definition.Id.ToLong()] = iterator.Current switch
                    {
                        InstanceBinding => ParameterBinding.Instance,
                        TypeBinding => ParameterBinding.Type,
                        _ => ParameterBinding.None
                    };
                }
            }
        }
        catch (Exception exception)
        {
            failures?.Add(exception);
        }

        return bindings;
    }

    public static List<ParameterInfo> Merged(List<ParameterInfo> catalog)
    {
        return catalog
            .GroupBy(info => info.Key)
            .Select(group => group.Aggregate(Merge))
            .OrderBy(info => info.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public ParameterInfo? TryInfoOf(Document document, long id, ParameterFacts facts,
        Dictionary<long, ParameterBinding> bindings, FailureTally? failures)
    {
        try
        {
            return InfoOf(document, id, facts, bindings);
        }
        catch (Exception exception)
        {
            failures?.Add(exception);
            return null;
        }
    }

    private static ParameterInfo Merge(ParameterInfo left, ParameterInfo right)
    {
        return left with
        {
            Origins = left.Origins | right.Origins,
            Bindings = left.Bindings | right.Bindings,
            Categories = left.Categories.Union(right.Categories),
            Group = left.Group ?? right.Group,
            Discipline = left.Discipline ?? right.Discipline,
            Families = left.Families.Concat(right.Families).Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToList()
        };
    }

    private static ParameterOrigin OriginOf(ParameterElement element, bool bound)
    {
        if (element is SharedParameterElement)
        {
            return ParameterOrigin.Shared;
        }

        return bound ? ParameterOrigin.Project : ParameterOrigin.Family;
    }

    private static string? Label(Dictionary<string, string?> known, ForgeTypeId id, Func<ForgeTypeId, string?> label)
    {
        if (!known.TryGetValue(id.TypeId, out var text))
        {
            text = label(id);
            known.Add(id.TypeId, text);
        }

        return text;
    }

    private ParameterInfo? InfoOf(Document document, long id, ParameterFacts facts,
        Dictionary<long, ParameterBinding> bindings)
    {
        if (id < 0)
        {
            return BuiltInInfo(document, (BuiltInParameter)(int)id, facts);
        }

        if (document.GetElement(id.ToElementId()) is not ParameterElement element
            || element.GetDefinition() is not { } definition)
        {
            return null;
        }

        var spec = SpecInfo.SpecOf(definition) ?? facts.Spec;
        facts.Group = SpecInfo.GroupOf(definition) ?? facts.Group;
        var bound = bindings.TryGetValue(id, out var binding);
        var origin = OriginOf(element, bound);
        var key = element is SharedParameterElement shared
            ? ParameterKey.Shared(definition.Name, shared.GuidValue)
            : ParameterKey.Named(definition.Name, SpecInfo.DataTypeOf(spec, spec is null ? facts.Storage : null));
        return Info(key, definition.Name, facts.Storage, spec, facts, origin, bound ? binding : facts.Observed);
    }

    private ParameterInfo BuiltInInfo(Document document, BuiltInParameter builtIn, ParameterFacts facts)
    {
        var storage = facts.Storage ?? document.get_TypeOfStorage(builtIn);
        var label = builtIn.ToLabel();
        return Info(ParameterKey.BuiltIn(label, builtIn.ToString()), label, storage, facts.Spec, facts,
            ParameterOrigin.BuiltIn, facts.Observed);
    }

    private ParameterInfo Info(ParameterKey key, string name, StorageType? storageType, ForgeTypeId? spec,
        ParameterFacts facts, ParameterOrigin origin, ParameterBinding binding)
    {
        var storage = storageType is { } known ? SpecInfo.StorageOf(known) : ParameterStorage.None;
        var measurable = SpecInfo.IsMeasurable(spec);
        var kind = ValueKinds.Infer(storage, SpecInfo.IsYesNo(spec), measurable, facts.HasNamedValues);
        return new ParameterInfo(key, name, kind, storage, spec?.TypeId, SpecInfo.SpecLabel(spec) ?? EnumText.Of(kind), origin,
            binding)
        {
            Categories = CategoryIdSet.Of(facts.Categories),
            Discipline = measurable ? Label(_disciplines, spec!, SpecInfo.DisciplineLabel) : null,
            Group = facts.Group is { } group ? Label(_groups, group, SpecInfo.GroupLabel) : null,
            Families = facts.Families.Count == 0
                ? []
                : facts.Families.OrderBy(family => family, StringComparer.CurrentCultureIgnoreCase).ToList()
        };
    }
}
