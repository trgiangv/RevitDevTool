using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;
using System.Diagnostics;

namespace RevitDevTool.Tools.ModelsContext.Parameters;

internal sealed class CatalogFacts
{
    private readonly CatalogInfoFactory _infos = new();
    private readonly Dictionary<long, ParameterFacts> _facts = [];
    private readonly HashSet<long> _sampled = [];
    private readonly HashSet<long> _sampledCategories = [];
    private readonly Dictionary<long, ElementFilter?> _nativeFilters = [];
    private readonly Dictionary<long, List<long>> _builtInsByCategory = [];

    public int AddFilterable(Document document, List<long> categories, FailureTally? failures)
    {
        foreach (var category in categories)
        {
            try
            {
                AddCategory(document, category);
            }
            catch (Exception exception)
            {
                failures?.Add(exception);
            }
        }

        return categories.Count;
    }

    public int Sample(Document document, long[] ids, int start, FailureTally? failures)
    {
        var watch = Stopwatch.StartNew();
        var index = start;
        while (index < ids.Length && index - start < ElementReader.SliceSize && watch.Elapsed < ElementReader.SliceTime)
        {
            var id = ids[index++];
            if (document.GetElement(id.ToElementId()) is { } element)
            {
                SampleOne(document, element, id, failures);
            }
        }

        return index;
    }

    public (List<ParameterInfo> Catalog, Dictionary<ParameterKey, IReadOnlyCollection<long>> Ids) Build(
        Document document,
        FailureTally? failures)
    {
        var bindings = CatalogInfoFactory.BindingsOf(document, failures);
        var catalog = new List<ParameterInfo>();
        var ids = new Dictionary<ParameterKey, HashSet<long>>();
        foreach (var pair in _facts)
        {
            if (EmptiedByVerification(pair.Key, pair.Value))
            {
                continue;
            }

            if (IsListed(pair.Key, pair.Value)
                && _infos.TryInfoOf(document, pair.Key, pair.Value, bindings, failures) is { } info)
            {
                catalog.Add(info);
                IdsFor(ids, info.Key).Add(pair.Key);
            }
        }

        return (CatalogInfoFactory.Merged(catalog),
            ids.ToDictionary(pair => pair.Key, pair => (IReadOnlyCollection<long>)pair.Value));
    }

    private static ElementFilter? NativeFilterOf(Document document, long id, ParameterFacts facts)
    {
        try
        {
            var parameterId = id.ToElementId();
            var rules = new List<Autodesk.Revit.DB.FilterRule>
            {
                ParameterFilterRuleFactory.CreateHasValueParameterRule(parameterId),
                ParameterFilterRuleFactory.CreateHasNoValueParameterRule(parameterId)
            };
            var storage = facts.Storage ?? document.get_TypeOfStorage((BuiltInParameter)(int)id);
            if (storage == StorageType.Integer)
            {
                rules.Add(ParameterFilterRuleFactory.CreateGreaterOrEqualRule(parameterId, int.MinValue));
            }
            else if (storage == StorageType.Double)
            {
                rules.Add(ParameterFilterRuleFactory.CreateGreaterOrEqualRule(parameterId, double.MinValue, 0));
            }

            return new LogicalOrFilter(rules.Select(rule => (ElementFilter)new ElementParameterFilter(rule)).ToList());
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static long SampleKeyOf(Element element, ElementId typeId, long id)
    {
        return typeId != ElementId.InvalidElementId
            ? typeId.ToLong()
            : element.Category?.Id.ToLong() ?? id;
    }

    private static bool IsListed(long id, ParameterFacts facts)
    {
        if (ParameterCatalogReader.Unfilterable.Contains(id))
        {
            return false;
        }

        return id >= 0 || facts.IsFilterable || facts.Storage is { } storage && storage != StorageType.None;
    }

    private static HashSet<long> IdsFor(Dictionary<ParameterKey, HashSet<long>> ids, ParameterKey key)
    {
        if (!ids.TryGetValue(key, out var known))
        {
            known = [];
            ids.Add(key, known);
        }

        return known;
    }

    private static bool CanCheckNames(ParameterFacts facts, Parameter parameter)
    {
        return !facts.NamesChecked
               && parameter.StorageType == StorageType.Integer
               && parameter.HasValue
               && !SpecInfo.IsYesNo(facts.Spec);
    }

    private void AddCategory(Document document, long category)
    {
        foreach (var id in ParameterFilterUtilities.GetFilterableParametersInCommon(document, [category.ToElementId()]))
        {
            AddParameter(category, id.ToLong());
        }
    }

    private void AddParameter(long category, long parameterId)
    {
        var facts = FactsOf(parameterId);
        facts.IsFilterable = true;
        facts.Categories.Add(category);
        if (parameterId < 0)
        {
            BuiltInsOf(category).Add(parameterId);
        }
    }

    private List<long> BuiltInsOf(long category)
    {
        if (!_builtInsByCategory.TryGetValue(category, out var list))
        {
            list = [];
            _builtInsByCategory.Add(category, list);
        }

        return list;
    }

    private bool EmptiedByVerification(long id, ParameterFacts facts)
    {
        if (id >= 0)
        {
            return false;
        }

        var removed = facts.Categories.RemoveWhere(category =>
            _sampledCategories.Contains(category) && !facts.Verified.Contains(category));
        return removed > 0 && facts.Categories.Count == 0;
    }

    private void SampleOne(Document document, Element element, long id, FailureTally? failures)
    {
        var typeId = element.GetTypeId();
        if (!_sampled.Add(SampleKeyOf(element, typeId, id)))
        {
            return;
        }

        try
        {
            var category = element.Category?.Id.ToLong();
            var type = document.GetElement(typeId);
            var family = (type as FamilySymbol)?.FamilyName;
            Observe(element, false, category, family);
            if (type is not null)
            {
                Observe(type, true, category, family);
            }

            if (category is { } sampledCategory)
            {
                VerifyFilterable(element, type, sampledCategory);
            }
        }
        catch (Exception exception)
        {
            failures?.Add(exception);
        }
    }

    private void VerifyFilterable(Element element, Element? type, long category)
    {
        _sampledCategories.Add(category);
        if (!_builtInsByCategory.TryGetValue(category, out var ids))
        {
            return;
        }

        foreach (var id in ids)
        {
            var facts = _facts[id];
            if (!facts.Verified.Contains(category)
                && (PassesNative(element, id, facts) || type is not null && PassesNative(type, id, facts)))
            {
                facts.Verified.Add(category);
            }
        }
    }

    private bool PassesNative(Element element, long id, ParameterFacts facts)
    {
        try
        {
            if (!_nativeFilters.TryGetValue(id, out var filter))
            {
                filter = NativeFilterOf(element.Document, id, facts);
                _nativeFilters.Add(id, filter);
            }

            return filter is null || filter.PassesFilter(element);
        }
        catch (Exception)
        {
            return true;
        }
    }

    private void Observe(Element element, bool onType, long? category, string? family)
    {
        foreach (Parameter parameter in element.Parameters)
        {
            ObserveParameter(parameter, onType, category, family);
        }
    }

    private void ObserveParameter(Parameter parameter, bool onType, long? category, string? family)
    {
        var parameterId = parameter.Id.ToLong();
        var facts = FactsOf(parameterId);
        if (category is { } id)
        {
            facts.Categories.Add(id);
            facts.Verified.Add(id);
        }

        if (family is not null && parameterId >= 0)
        {
            facts.Families.Add(family);
        }

        facts.Storage ??= parameter.StorageType;
        facts.Spec ??= SpecInfo.SpecOf(parameter.Definition);
        if (!facts.GroupChecked)
        {
            facts.GroupChecked = true;
            facts.Group = SpecInfo.GroupOf(parameter.Definition);
        }

        facts.Observed |= onType ? ParameterBinding.Type : ParameterBinding.Instance;
        if (CanCheckNames(facts, parameter) && parameter.AsValueString() is { Length: > 0 } text)
        {
            facts.NamesChecked = true;
            facts.HasNamedValues = ParameterDataTypes.IsNamedValue(text);
        }
    }

    private ParameterFacts FactsOf(long id)
    {
        if (!_facts.TryGetValue(id, out var facts))
        {
            facts = new ParameterFacts();
            _facts.Add(id, facts);
        }

        return facts;
    }
}
