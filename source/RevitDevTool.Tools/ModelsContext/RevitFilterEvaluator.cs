using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using Nice3point.Revit.Extensions;
using System.Diagnostics;
using DocumentVersion = DevTools.ModelsContext.Model.DocumentVersion;
using RuleNode = DevTools.ModelsContext.Model.FilterRule;

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
        var warmed = await WarmAsync(document, filter, cancellationToken).ConfigureAwait(false);
        var raw = new Dictionary<ParameterKey, IReadOnlyDictionary<string, long[]>>();
        while (true)
        {
            var prepared = await RevitProfiler.RunAsync("Filter compile",
                application => Prepare(DocumentRegistry.Require(application, document), filter, scope, raw, warmed),
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

    private async Task<Warmed> WarmAsync(DocumentKey document, FilterDefinition filter, CancellationToken cancellationToken)
    {
        var categories = Categories(filter.Groups);
        var warmed = await RevitProfiler.RunAsync("Filter candidates", application =>
        {
            var resolved = DocumentRegistry.Require(application, document);
            var timer = TraceTimer.Start(RevitProfiler.Logger);
            CandidatesOf(resolved.Target, DocumentRegistry.SourceOf(resolved), ElementReader.VersionOf(resolved.Target),
                filter.Groups, out var cached);
            return new Warmed(cached ? 0 : timer.ElapsedMs, !cached);
        }, cancellationToken).ConfigureAwait(false);
        var typed = false;
        var presence = PresenceKeysOf(filter.Root);
        foreach (var key in KeysOf(filter.Root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var located = await RevitProfiler.RunAsync("Filter parameter", application =>
            {
                var resolved = DocumentRegistry.Require(application, document);
                var source = DocumentRegistry.SourceOf(resolved);
                var version = ElementReader.VersionOf(resolved.Target);
                return cache.FindResolved(source, key, filter.Groups, version) is { } cached
                    ? new Located(source, version, cached, true)
                    : new Located(source, version,
                        ParameterLocator.Locate(resolved.Target, key, categories, cache.FindIds(source, version, key)), false);
            }, cancellationToken).ConfigureAwait(false);
            var parameters = located.Parameters;
            if (!located.Done)
            {
                var typedParameters = await RevitProfiler.RunAsync("Filter parameter types", application =>
                        ParameterLocator.WithTypes(DocumentRegistry.Require(application, document).Target, key, categories,
                            located.Parameters),
                    cancellationToken).ConfigureAwait(false);
                parameters = await RevitProfiler.RunAsync("Filter parameter lacking", application =>
                {
                    var all = ParameterLocator.WithLacking(DocumentRegistry.Require(application, document).Target, key,
                        categories, typedParameters);
                    cache.StoreResolved(located.Source, key, filter.Groups, located.Version, all);
                    return all;
                }, cancellationToken).ConfigureAwait(false);
            }

            if (presence.Contains(key) && parameters.Any(parameter => !parameter.SilentChecked))
            {
                parameters = await SilentAsync(document, key, filter.Groups, categories, located, parameters, cancellationToken)
                    .ConfigureAwait(false);
            }

            typed |= parameters.Any(parameter => !parameter.Types.IsEmpty || parameter.Silent.Count > 0);
        }

        if (typed)
        {
            await RevitProfiler.RunAsync("Filter types", application =>
            {
                var resolved = DocumentRegistry.Require(application, document);
                CandidatesOf(resolved.Target, DocumentRegistry.SourceOf(resolved), ElementReader.VersionOf(resolved.Target),
                    filter.Groups, out _).MembersIn(resolved.Target);
            }, cancellationToken).ConfigureAwait(false);
        }

        return warmed;
    }

    private async Task<IReadOnlyList<ResolvedParameter>> SilentAsync(
        DocumentKey document,
        ParameterKey key,
        IReadOnlyList<long> groups,
        ICollection<ElementId> categories,
        Located located,
        IReadOnlyList<ResolvedParameter> parameters,
        CancellationToken cancellationToken)
    {
        var updated = new List<ResolvedParameter>();
        foreach (var parameter in parameters)
        {
            if (parameter.SilentChecked)
            {
                updated.Add(parameter);
                continue;
            }

            var reader = await RevitProfiler.RunAsync("Filter parameter silent", application =>
                    SilentReader.For(DocumentRegistry.Require(application, document).Target, categories, key, parameter),
                cancellationToken).ConfigureAwait(false);
            var index = 0;
            while (index < reader.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var start = index;
                index = await RevitProfiler.RunAsync("Filter silent slice", application =>
                        reader.AddSlice(DocumentRegistry.Require(application, document).Target, start),
                    cancellationToken).ConfigureAwait(false);
            }

            updated.Add(reader.ToResolved());
        }

        cache.StoreResolved(located.Source, key, groups, located.Version, updated);
        return updated;
    }

    private static HashSet<ParameterKey> PresenceKeysOf(FilterNode root)
    {
        var keys = new HashSet<ParameterKey>();
        var pending = new Stack<FilterNode>([root]);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case FilterSet set:
                    foreach (var child in set.Children)
                    {
                        pending.Push(child);
                    }

                    break;
                case RuleNode { Operator: FilterOperator.NotEmpty or FilterOperator.Empty or FilterOperator.Defined or FilterOperator.Undefined } rule:
                    keys.Add(rule.Parameter);
                    break;
            }
        }

        return keys;
    }

    private static List<ParameterKey> KeysOf(FilterNode root)
    {
        var keys = new List<ParameterKey>();
        var pending = new Stack<FilterNode>([root]);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case FilterSet set:
                    for (var index = set.Children.Count - 1; index >= 0; index--)
                    {
                        pending.Push(set.Children[index]);
                    }

                    break;
                case RuleNode rule when !keys.Contains(rule.Parameter):
                    keys.Add(rule.Parameter);
                    break;
            }
        }

        return keys;
    }

    private Prepared Prepare(
        ResolvedDocument resolved,
        FilterDefinition filter,
        IReadOnlyCollection<long>? scope,
        Dictionary<ParameterKey, IReadOnlyDictionary<string, long[]>> raw,
        Warmed warmed)
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
        var collected = candidates is not null && (!cached || warmed.Collected);
        RevitProfiler.Logger.FilterCompiled(source.Label, compiler.Rules, compiler.Unresolved, ids.Length,
            plan.HasExactPass, Math.Round(timer.ElapsedMs - candidatesMs, 1), Math.Round(candidatesMs + warmed.CandidatesMs, 1),
            candidates is null ? "skipped" : collected ? "collected" : "cached");
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

    private sealed record Warmed(double CandidatesMs, bool Collected);

    private sealed record Located(
        SourceId Source,
        DocumentVersion? Version,
        IReadOnlyList<ResolvedParameter> Parameters,
        bool Done);

    private sealed record Prepared(
        SourceId Source,
        long[] Candidates,
        FilterPlan? Plan,
        IReadOnlyList<ParameterKey> MissingValues);
}
