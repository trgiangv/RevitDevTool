using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Model;
using Nice3point.Revit.Extensions;
using System.Diagnostics;

namespace RevitDevTool.Tools.ModelsContext;

internal static class ParameterCatalogReader
{
    private const int CategoriesPerSlice = 20;

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
            var chunk = categories.Skip(start).Take(CategoriesPerSlice).ToList();
            await RevitProfiler.RunAsync("Catalog slice", application =>
                    facts.AddFilterable(DocumentRegistry.RequireSource(application, source), chunk, failures),
                cancellationToken, stats).ConfigureAwait(false);
        }

        stats?.Flush(source, categories.Count);
        var ids = await RevitProfiler.RunAsync("Catalog sample ids", application =>
                ElementReader.CollectIds(DocumentRegistry.RequireSource(application, source)),
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
        private readonly Dictionary<long, Facts> _facts = [];
        private readonly HashSet<long> _sampled = [];

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
                if (document.GetElement(id.ToElementId()) is not { } element)
                {
                    continue;
                }

                var typeId = element.GetTypeId();
                var sampleKey = typeId != ElementId.InvalidElementId
                    ? typeId.ToLong()
                    : element.Category?.Id.ToLong() ?? id;
                if (!_sampled.Add(sampleKey))
                {
                    continue;
                }

                try
                {
                    var category = element.Category?.Id.ToLong();
                    Observe(element, false, category);
                    if (document.GetElement(typeId) is { } type)
                    {
                        Observe(type, true, category);
                    }
                }
                catch (Exception exception)
                {
                    failures?.Add(exception);
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
                if (pair.Key < 0 && !pair.Value.IsFilterable)
                {
                    continue;
                }

                try
                {
                    if (InfoOf(document, pair.Key, pair.Value, bindings) is { } info)
                    {
                        catalog.Add(info);
                        if (!ids.TryGetValue(info.Key, out var known))
                        {
                            known = [];
                            ids.Add(info.Key, known);
                        }

                        known.Add(pair.Key);
                    }
                }
                catch (Exception exception)
                {
                    failures?.Add(exception);
                }
            }

            return (catalog
                .GroupBy(info => info.Key)
                .Select(group => group.Aggregate((left, right) => left with
                {
                    Origins = left.Origins | right.Origins,
                    Bindings = left.Bindings | right.Bindings,
                    Categories = left.Categories.Union(right.Categories)
                }))
                .OrderBy(info => info.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList(), ids.ToDictionary(pair => pair.Key, pair => (IReadOnlyCollection<long>)pair.Value));
        }

        private static ParameterInfo? InfoOf(Document document, long id, Facts facts,
            Dictionary<long, ParameterBinding> bindings)
        {
            if (id < 0)
            {
                var builtIn = (BuiltInParameter)(int)id;
                var storage = facts.Storage ?? document.get_TypeOfStorage(builtIn);
                var label = LabelUtils.GetLabelFor(builtIn);
                return Info(ParameterKey.BuiltIn(label, builtIn.ToString()), label, storage, facts.Spec, facts,
                    ParameterOrigin.BuiltIn, facts.Observed);
            }

            if (document.GetElement(id.ToElementId()) is not ParameterElement element
                || element.GetDefinition() is not { } definition)
            {
                return null;
            }

            var spec = ParameterResolver.SpecOf(definition) ?? facts.Spec;
            var bound = bindings.TryGetValue(id, out var binding);
            var origin = element is SharedParameterElement
                ? ParameterOrigin.Shared
                : bound
                    ? ParameterOrigin.Project
                    : ParameterOrigin.Family;
            var key = element is SharedParameterElement shared
                ? ParameterKey.Shared(definition.Name, shared.GuidValue)
                : ParameterKey.Named(definition.Name, ParameterResolver.DataTypeOf(spec, spec is null ? facts.Storage : null));
            return Info(key, definition.Name, facts.Storage, spec, facts, origin, bound ? binding : facts.Observed);
        }

        private static ParameterInfo Info(ParameterKey key, string name, StorageType? storageType, ForgeTypeId? spec,
            Facts facts, ParameterOrigin origin, ParameterBinding binding)
        {
            var storage = storageType is { } known ? ParameterResolver.StorageOf(known) : ParameterStorage.None;
            var kind = ValueKinds.Infer(storage, ParameterResolver.IsYesNo(spec), ParameterResolver.IsMeasurable(spec),
                facts.HasNamedValues);
            return new ParameterInfo(key, name, kind, storage, spec?.TypeId, ParameterResolver.SpecLabel(spec) ?? EnumText.Of(kind), origin,
                binding)
            {
                Categories = CategoryIdSet.Of(facts.Categories)
            };
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

        private void Observe(Element element, bool onType, long? category)
        {
            foreach (Parameter parameter in element.Parameters)
            {
                var facts = FactsOf(parameter.Id.ToLong());
                if (category is { } id)
                {
                    facts.Categories.Add(id);
                }

                facts.Storage ??= parameter.StorageType;
                facts.Spec ??= ParameterResolver.SpecOf(parameter.Definition);
                facts.Observed |= onType ? ParameterBinding.Type : ParameterBinding.Instance;
                if (!facts.NamesChecked && parameter.StorageType == StorageType.Integer && parameter.HasValue
                    && !ParameterResolver.IsYesNo(facts.Spec)
                    && parameter.AsValueString() is { Length: > 0 } text)
                {
                    facts.NamesChecked = true;
                    facts.HasNamedValues = ParameterResolver.IsNamedValue(text);
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

        public HashSet<long> Categories { get; } = [];
    }
}
