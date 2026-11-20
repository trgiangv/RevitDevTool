using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Model;
using Nice3point.Revit.Extensions;
using System.Diagnostics;

namespace RevitDevTool.Tools.ModelsContext;

internal static class ParameterCatalogReader
{
    private const int CategoriesPerSlice = 20;

    public static readonly HashSet<long> Unfilterable = [(long)BuiltInParameter.REBAR_ELEM_LENGTH];

    public static async Task<IReadOnlyList<ParameterInfo>> ReadAsync(
        SourceId source,
        ParameterCache cache,
        CancellationToken cancellationToken)
    {
        var (categories, version) = await RevitProfiler.RunAsync("Catalog categories", application =>
        {
            var document = DocumentRegistry.RequireSource(application, source);
            return (FilterableCategories(document), ElementReader.VersionOf(document));
        }, cancellationToken).ConfigureAwait(false);
        if (cache.FindCatalog(source, version) is { } cached)
        {
            return cached;
        }

        var facts = new CatalogFacts();
        var failures = FailureTally.Start(RevitProfiler.Logger);
        var stats = ProfileStats.Start("Catalog");
        for (var start = 0; start < categories.Count; start += CategoriesPerSlice)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = categories.GetRange(start, Math.Min(CategoriesPerSlice, categories.Count - start));
            await RevitProfiler.RunAsync("Catalog slice", application =>
                    facts.AddFilterable(DocumentRegistry.RequireSource(application, source), chunk, failures),
                cancellationToken, stats).ConfigureAwait(false);
        }

        stats?.Flush(source, categories.Count);
        var ids = await RevitProfiler.RunAsync("Catalog sample ids", application =>
                ElementReader.CollectAnyOption(DocumentRegistry.RequireSource(application, source)),
            cancellationToken).ConfigureAwait(false);
        var samples = ProfileStats.Start("Catalog samples");
        var index = 0;
        while (index < ids.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = index;
            index = await RevitProfiler.RunAsync("Catalog sample slice", application =>
                    facts.Sample(DocumentRegistry.RequireSource(application, source), ids, start, failures),
                cancellationToken, samples).ConfigureAwait(false);
        }

        samples?.Flush(source, ids.Length);
        var (catalog, keyIds) = await RevitProfiler.RunAsync("Catalog build", application =>
                facts.Build(DocumentRegistry.RequireSource(application, source), failures),
            cancellationToken).ConfigureAwait(false);
        failures?.Flush("Catalog", source, catalog.Count);
        cache.StoreCatalog(source, version, catalog, keyIds);
        return catalog;
    }

    private static List<long> FilterableCategories(Document document)
    {
        var filterable = new HashSet<long>(ParameterFilterUtilities.GetAllFilterableCategories().Select(id => id.ToLong()));
        return ElementReader.ModelCategoryIds(document).Select(id => id.ToLong()).Where(filterable.Contains).ToList();
    }

    private sealed class CatalogFacts
    {
        private readonly Dictionary<string, string?> _disciplines = new(StringComparer.Ordinal);
        private readonly Dictionary<long, Facts> _facts = [];
        private readonly Dictionary<string, string?> _groups = new(StringComparer.Ordinal);
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
                    foreach (var id in ParameterFilterUtilities.GetFilterableParametersInCommon(document,
                                 [category.ToElementId()]))
                    {
                        var facts = FactsOf(id.ToLong());
                        facts.IsFilterable = true;
                        facts.Categories.Add(category);
                        if (id.ToLong() < 0)
                        {
                            if (!_builtInsByCategory.TryGetValue(category, out var list))
                            {
                                list = [];
                                _builtInsByCategory.Add(category, list);
                            }

                            list.Add(id.ToLong());
                        }
                    }
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
            var bindings = BindingsOf(document, failures);
            var catalog = new List<ParameterInfo>();
            var ids = new Dictionary<ParameterKey, HashSet<long>>();
            foreach (var pair in _facts)
            {
                if (pair.Key < 0)
                {
                    var removed = pair.Value.Categories.RemoveWhere(category =>
                        _sampledCategories.Contains(category) && !pair.Value.Verified.Contains(category));
                    if (removed > 0 && pair.Value.Categories.Count == 0)
                    {
                        continue;
                    }
                }

                if (IsListed(pair.Key, pair.Value)
                    && TryInfoOf(document, pair.Key, pair.Value, bindings, failures) is { } info)
                {
                    catalog.Add(info);
                    IdsFor(ids, info.Key).Add(pair.Key);
                }
            }

            return (Merged(catalog), ids.ToDictionary(pair => pair.Key, pair => (IReadOnlyCollection<long>)pair.Value));
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

        private bool PassesNative(Element element, long id, Facts facts)
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

        private static ElementFilter? NativeFilterOf(Document document, long id, Facts facts)
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

        private static bool IsListed(long id, Facts facts)
        {
            if (Unfilterable.Contains(id))
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

        private static List<ParameterInfo> Merged(List<ParameterInfo> catalog)
        {
            return catalog
                .GroupBy(info => info.Key)
                .Select(group => group.Aggregate(Merge))
                .OrderBy(info => info.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
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

        private ParameterInfo? TryInfoOf(Document document, long id, Facts facts,
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

        private ParameterInfo? InfoOf(Document document, long id, Facts facts,
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

        private ParameterInfo BuiltInInfo(Document document, BuiltInParameter builtIn, Facts facts)
        {
            var storage = facts.Storage ?? document.get_TypeOfStorage(builtIn);
            var label = builtIn.ToLabel();
            return Info(ParameterKey.BuiltIn(label, builtIn.ToString()), label, storage, facts.Spec, facts,
                ParameterOrigin.BuiltIn, facts.Observed);
        }

        private static ParameterOrigin OriginOf(ParameterElement element, bool bound)
        {
            if (element is SharedParameterElement)
            {
                return ParameterOrigin.Shared;
            }

            return bound ? ParameterOrigin.Project : ParameterOrigin.Family;
        }

        private ParameterInfo Info(ParameterKey key, string name, StorageType? storageType, ForgeTypeId? spec,
            Facts facts, ParameterOrigin origin, ParameterBinding binding)
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

        private static string? Label(Dictionary<string, string?> known, ForgeTypeId id, Func<ForgeTypeId, string?> label)
        {
            if (!known.TryGetValue(id.TypeId, out var text))
            {
                text = label(id);
                known.Add(id.TypeId, text);
            }

            return text;
        }

        private static Dictionary<long, ParameterBinding> BindingsOf(Document document, FailureTally? failures)
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

        private void Observe(Element element, bool onType, long? category, string? family)
        {
            foreach (Parameter parameter in element.Parameters)
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
                if (!facts.NamesChecked && parameter.StorageType == StorageType.Integer && parameter.HasValue
                    && !SpecInfo.IsYesNo(facts.Spec)
                    && parameter.AsValueString() is { Length: > 0 } text)
                {
                    facts.NamesChecked = true;
                    facts.HasNamedValues = ParameterDataTypes.IsNamedValue(text);
                }
            }
        }

        private Facts FactsOf(long id)
        {
            if (!_facts.TryGetValue(id, out var facts))
            {
                facts = new Facts();
                _facts.Add(id, facts);
            }

            return facts;
        }
    }

    private sealed class Facts
    {
        public bool IsFilterable { get; set; }

        public StorageType? Storage { get; set; }

        public ForgeTypeId? Spec { get; set; }

        public ParameterBinding Observed { get; set; }

        public bool NamesChecked { get; set; }

        public bool HasNamedValues { get; set; }

        public bool GroupChecked { get; set; }

        public ForgeTypeId? Group { get; set; }

        public HashSet<long> Categories { get; } = [];

        public HashSet<long> Verified { get; } = [];

        public HashSet<string> Families { get; } = new(StringComparer.Ordinal);
    }
}
