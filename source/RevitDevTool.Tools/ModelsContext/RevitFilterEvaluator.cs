using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using Nice3point.Revit.Extensions;
using System.Diagnostics;
using DocumentVersion = DevTools.ModelsContext.Model.DocumentVersion;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class RevitFilterEvaluator(ParameterCache cache) : IFilterEvaluator
{
    public const int MatchBatch = 500;
    public const int FirstBatch = 100;
    public const int MinBatch = 25;

    public async Task<IReadOnlyCollection<long>> EvaluateAsync(
        DocumentKey document,
        FilterDefinition filter,
        IReadOnlyCollection<long>? scope,
        CancellationToken cancellationToken)
    {
        var raw = new Dictionary<ParameterKey, IReadOnlyDictionary<string, long[]>>();
        while (true)
        {
            var prepared = await RevitProfiler.RunAsync("Filter candidates",
                application => Prepare(DocumentRegistry.Require(application, document), filter, scope, raw),
                cancellationToken).ConfigureAwait(false);
            if (prepared.Plan is not { } plan)
            {
                foreach (var key in prepared.MissingValues)
                {
                    var values = await ValuesAsync(prepared.Source, key, filter.Groups,
                        application => DocumentRegistry.Require(application, document).Target,
                        cancellationToken).ConfigureAwait(false);
                    raw[key] = values.RawValues;
                }

                continue;
            }

            return await MatchInSlicesAsync(document, prepared.Source.Label, prepared.Candidates, plan.Match,
                cancellationToken, () => plan.NativeCount).ConfigureAwait(false);
        }
    }

    private Prepared Prepare(
        ResolvedDocument resolved,
        FilterDefinition filter,
        IReadOnlyCollection<long>? scope,
        Dictionary<ParameterKey, IReadOnlyDictionary<string, long[]>> raw)
    {
        var timer = TraceTimer.Start(RevitProfiler.Logger);
        var categories = Categories(filter.Groups);
        var source = DocumentRegistry.SourceOf(resolved);
        var version = ElementReader.VersionOf(resolved.Target);
        var candidatesMs = 0.0;
        var cached = false;
        CandidateSet? candidates = null;
        CandidateSet Candidates()
        {
            if (candidates is null)
            {
                var started = timer.ElapsedMs;
                candidates = CandidatesOf(resolved.Target, source, version, filter.Groups, out cached);
                candidatesMs += timer.ElapsedMs - started;
            }

            return candidates;
        }

        var compiler = new FilterCompiler(source.Label,
            key => Resolve(resolved.Target, source, version, key, filter.Groups, categories),
            key => raw.TryGetValue(key, out var known) ? known : cache.FindValues(source, key, filter.Groups, version)?.RawValues,
            RuleValueParser.For(resolved.Model.GetUnits()),
            new TypeFilters(() => Candidates().MembersIn(resolved.Target)));
        var plan = compiler.Plan(filter.Root);
        if (compiler.MissingValues.Count > 0)
        {
            return new Prepared(source, [], null, [.. compiler.MissingValues]);
        }

        var ids = plan.Native is Compiled.MatchNone ? Array.Empty<long>() : Candidates().InScope(scope);
        RevitProfiler.Logger.FilterCompiled(source.Label, compiler.Rules, compiler.Unresolved, ids.Length,
            plan.HasExactPass, Math.Round(timer.ElapsedMs - candidatesMs, 1), Math.Round(candidatesMs, 1), candidates is null ? "skipped" : cached ? "cached" : "collected");
        return new Prepared(source, ids, plan, []);
    }

    private CandidateSet CandidatesOf(
        Document target,
        SourceId source,
        DocumentVersion? version,
        IReadOnlyCollection<long> groups,
        out bool cached)
    {
        return cache.CandidatesOf(source, groups, version, () => ElementReader.CandidatesOf(target, Categories(groups)),
            out cached);
    }

    private IReadOnlyList<ResolvedParameter> Resolve(
        Document target,
        SourceId source,
        DocumentVersion? version,
        ParameterKey key,
        IReadOnlyList<long> groups,
        ICollection<ElementId> categories)
    {
        if (cache.FindResolved(source, key, groups, version) is { } cached)
        {
            return cached;
        }

        var resolved = ParameterLocator.Resolve(target, key, categories, cache.FindIds(source, version, key));
        cache.StoreResolved(source, key, groups, version, resolved);
        return resolved;
    }

    internal static async Task<IReadOnlyCollection<long>> MatchInSlicesAsync(
        DocumentKey document,
        string label,
        long[] candidates,
        Func<Document, List<ElementId>, List<long>> match,
        CancellationToken cancellationToken,
        Func<int>? native = null)
    {
        var result = new List<long>();
        var stats = ProfileStats.Start("Match");
        var pace = new SlicePace(FirstBatch, MinBatch, MatchBatch);
        var index = 0;
        while (index < candidates.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = index;
            var (next, matches) = await RevitProfiler.RunAsync("Match slice",
                application => MatchSlice(DocumentRegistry.Require(application, document).Target, candidates, start, match,
                    pace),
                cancellationToken, stats).ConfigureAwait(false);
            result.AddRange(matches);
            index = next;
        }

        if (stats is not null)
        {
            stats.Flush(label, candidates.Length);
            RevitProfiler.Logger.Matched(label, candidates.Length, native?.Invoke() ?? result.Count, result.Count);
        }

        return result;
    }

    private static (int Next, List<long> Matches) MatchSlice(
        Document target,
        long[] candidates,
        int start,
        Func<Document, List<ElementId>, List<long>> match,
        SlicePace pace)
    {
        var watch = Stopwatch.StartNew();
        var limit = Math.Min(candidates.Length, start + ElementReader.SliceSize);
        var matches = new List<long>();
        var position = start;
        while (position < limit && watch.Elapsed < ElementReader.SliceTime)
        {
            var batchStarted = watch.Elapsed;
            var end = Math.Min(limit, position + pace.Next(ElementReader.SliceTime - batchStarted));
            var chunk = new List<ElementId>(end - position);
            for (var i = position; i < end; i++)
            {
                var id = candidates[i].ToElementId();
                if (target.GetElement(id) is not null)
                {
                    chunk.Add(id);
                }
            }

            if (chunk.Count > 0)
            {
                matches.AddRange(match(target, chunk));
            }

            pace.Record(end - position, watch.Elapsed - batchStarted);
            position = end;
        }

        return (position, matches);
    }

    public async Task<ParameterValueCounts> CountValuesAsync(
        SourceId source,
        ParameterKey parameter,
        IReadOnlyCollection<long> groups,
        CancellationToken cancellationToken)
    {
        var values = await ValuesAsync(source, parameter, groups,
            application => DocumentRegistry.RequireSource(application, source), cancellationToken).ConfigureAwait(false);
        return new ParameterValueCounts(values.Total, values.Counts, values.Numbers);
    }

    public Task<string?> FormatExampleAsync(DocumentKey model, string spec, CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Example value", application =>
            RuleValueParser.Example(DocumentRegistry.Require(application, model).Model.GetUnits(), spec), cancellationToken);
    }

    private async Task<ValueEntry> ValuesAsync(
        SourceId source,
        ParameterKey parameter,
        IReadOnlyCollection<long> groups,
        Func<UIApplication, Document> target,
        CancellationToken cancellationToken)
    {
        var (candidates, version, cached) = await RevitProfiler.RunAsync("Value candidates", application =>
        {
            var document = target(application);
            var stamp = ElementReader.VersionOf(document);
            var entry = cache.FindValues(source, parameter, groups, stamp);
            var set = entry is null ? CandidatesOf(document, source, stamp, groups, out _) : null;
            return (set, stamp, entry);
        }, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        var tally = new ValueReader(parameter, candidates!);
        var stats = ProfileStats.Start("Values");
        var index = 0;
        while (index < candidates!.Ids.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = index;
            index = await RevitProfiler.RunAsync("Value slice",
                application => tally.AddSlice(target(application), start),
                cancellationToken, stats).ConfigureAwait(false);
        }

        stats?.Flush(source, candidates.Ids.Length);
        var values = tally.ToEntry();
        cache.StoreValues(source, parameter, groups, version, values);
        return values;
    }


    private static List<ElementId> Categories(IEnumerable<long> groups)
    {
        return groups.Select(id => id.ToElementId()).ToList();
    }

    private sealed record Prepared(
        SourceId Source,
        long[] Candidates,
        FilterPlan? Plan,
        IReadOnlyList<ParameterKey> MissingValues);
}
