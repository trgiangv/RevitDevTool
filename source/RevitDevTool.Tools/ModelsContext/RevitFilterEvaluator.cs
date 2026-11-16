using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using Nice3point.Revit.Extensions;
using System.Diagnostics;
using DocumentVersion = DevTools.ModelsContext.Model.DocumentVersion;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class RevitFilterEvaluator(ParameterCache cache) : IFilterEvaluator
{
    public async Task<IReadOnlyCollection<long>> EvaluateAsync(
        DocumentKey document,
        FilterDefinition filter,
        IReadOnlyCollection<long>? scope,
        CancellationToken cancellationToken)
    {
        var (candidates, plan) = await RevitProfiler.RunAsync("Filter candidates", application =>
        {
            var resolved = DocumentRegistry.Require(application, document);
            var categories = Categories(filter.Groups);
            var source = SourceOf(resolved);
            var version = ElementReader.VersionOf(resolved.Target);
            var compiler = new FilterCompiler(resolved.Target, resolved.Model.GetUnits(), categories,
                parameter => EnumValuesOf(resolved, source, version, parameter.Key, filter.Groups, categories),
                key => cache.FindIds(source, version, key));
            var plan = compiler.Plan(filter.Root);
            var ids = plan.Native is Compiled.MatchNone
                ? Array.Empty<long>()
                : InScope(ElementReader.CollectIds(resolved.Target, categories), scope);
            return (ids, plan);
        }, cancellationToken).ConfigureAwait(false);
        return await MatchInSlicesAsync(document, candidates, plan.Match, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<IReadOnlyCollection<long>> MatchInSlicesAsync(
        DocumentKey document,
        long[] candidates,
        Func<Document, List<ElementId>, List<long>> match,
        CancellationToken cancellationToken)
    {
        var result = new List<long>();
        var stats = ProfileStats.Start("Match");
        var index = 0;
        while (index < candidates.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = index;
            var (next, matches) = await RevitProfiler.RunAsync("Match slice", application =>
            {
                var target = DocumentRegistry.Require(application, document).Target;
                var end = Math.Min(candidates.Length, start + ElementReader.SliceSize);
                var chunk = candidates.Skip(start).Take(end - start)
                    .Select(id => id.ToElementId())
                    .Where(id => target.GetElement(id) is not null)
                    .ToList();
                return (end, chunk.Count == 0 ? new List<long>() : match(target, chunk));
            }, cancellationToken, stats).ConfigureAwait(false);
            result.AddRange(matches);
            index = next;
        }

        stats?.Flush(document, candidates.Length);
        return result;
    }

    internal static long[] InScope(IEnumerable<long> ids, IReadOnlyCollection<long>? scope)
    {
        if (scope is null)
        {
            return ids.ToArray();
        }

        var inScope = scope as ISet<long> ?? new HashSet<long>(scope);
        return ids.Where(inScope.Contains).ToArray();
    }

    public async Task<ParameterValueCounts> CountValuesAsync(
        SourceId source,
        ParameterKey parameter,
        IReadOnlyCollection<long> groups,
        CancellationToken cancellationToken)
    {
        var (candidates, version, cached) = await RevitProfiler.RunAsync("Value candidates", application =>
        {
            var document = DocumentRegistry.RequireSource(application, source);
            var stamp = ElementReader.VersionOf(document);
            var entry = cache.FindValues(source, parameter, groups, stamp);
            var ids = entry is null ? ElementReader.CollectIds(document, Categories(groups)) : Array.Empty<long>();
            return (ids, stamp, entry);
        }, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return new ParameterValueCounts(cached.Total, cached.Counts);
        }

        var tally = new ValueTally(parameter);
        var stats = ProfileStats.Start("Values");
        var index = 0;
        while (index < candidates.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = index;
            index = await RevitProfiler.RunAsync("Value slice", application =>
                    tally.AddSlice(DocumentRegistry.RequireSource(application, source), candidates, start),
                cancellationToken, stats).ConfigureAwait(false);
        }

        stats?.Flush(source, candidates.Length);
        var values = tally.ToEntry(candidates.Length);
        cache.StoreValues(source, parameter, groups, version, values);
        return new ParameterValueCounts(values.Total, values.Counts);
    }

    private static SourceId SourceOf(ResolvedDocument resolved)
    {
        return resolved.Link is { } link ? DocumentRegistry.LinkSourceOf(link) : DocumentRegistry.SourceOf(resolved.Model);
    }

    private IReadOnlyDictionary<string, int> EnumValuesOf(
        ResolvedDocument resolved,
        SourceId source,
        DocumentVersion? version,
        ParameterKey parameter,
        IReadOnlyCollection<long> groups,
        ICollection<ElementId> categories)
    {
        if (cache.FindValues(source, parameter, groups, version) is { } cached)
        {
            return cached.EnumValues;
        }

        var candidates = ElementReader.CollectIds(resolved.Target, categories);
        var tally = new ValueTally(parameter);
        var index = 0;
        while (index < candidates.Length)
        {
            index = tally.AddSlice(resolved.Target, candidates, index, false);
        }

        var values = tally.ToEntry(candidates.Length);
        cache.StoreValues(source, parameter, groups, version, values);
        return values.EnumValues;
    }

    private static List<ElementId> Categories(IEnumerable<long> groups)
    {
        return groups.Select(id => id.ToElementId()).ToList();
    }

    private sealed class ValueTally(ParameterKey parameter)
    {
        private readonly Dictionary<string, int> _enumValues = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

        public int AddSlice(Document document, long[] candidates, int start, bool timed = true)
        {
            var watch = Stopwatch.StartNew();
            var position = start;
            while (position < candidates.Length && position - start < ElementReader.SliceSize
                                                 && (!timed || watch.Elapsed < ElementReader.SliceTime))
            {
                Add(document, candidates[position]);
                position++;
            }

            return position;
        }

        public ValueEntry ToEntry(int total)
        {
            return new ValueEntry(total, _counts, _enumValues);
        }

        private void Add(Document document, long id)
        {
            if (document.GetElement(id.ToElementId()) is not { } element
                || ParameterResolver.Find(element, parameter) is not { } value
                || ParameterResolver.ValueText(document, value) is not { } text)
            {
                return;
            }

            _counts[text] = _counts.TryGetValue(text, out var count) ? count + 1 : 1;
            if (value.StorageType == StorageType.Integer && !_enumValues.ContainsKey(text))
            {
                _enumValues[text] = value.AsInteger();
            }
        }
    }
}
