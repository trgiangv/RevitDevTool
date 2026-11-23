using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Pane;
using RevitDevTool.Tools.ModelsContext.Parameters;
using System.Diagnostics;
using DocumentVersion = DevTools.ModelsContext.Documents.DocumentVersion;
using RuleNode = DevTools.ModelsContext.Filters.FilterRule;

namespace RevitDevTool.Tools.ModelsContext.Filters;

internal sealed class RevitFilterEvaluator(ParameterCache cache) : IFilterEvaluator
{
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

            return await SliceMatcher.MatchInSlicesAsync(document, prepared.Source.Label, prepared.Candidates, plan.Match,
                cancellationToken, () => plan.NativeCount).ConfigureAwait(false);
        }
    }

    private async Task<Warmed> WarmAsync(DocumentKey document, FilterDefinition filter, CancellationToken cancellationToken)
    {
        var categories = Categories(filter.Groups);
        var warmed = await WarmCandidatesAsync(document, filter, cancellationToken).ConfigureAwait(false);
        var typed = false;
        var presence = FilterKeys.Presence(filter.Root);
        foreach (var key in FilterKeys.All(filter.Root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            typed |= await WarmKeyAsync(document, filter, categories, key, presence.Contains(key), cancellationToken)
                .ConfigureAwait(false);
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

    private Task<Warmed> WarmCandidatesAsync(DocumentKey document, FilterDefinition filter, CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Filter candidates", application =>
        {
            var resolved = DocumentRegistry.Require(application, document);
            var timer = TraceTimer.Start(RevitProfiler.Logger);
            CandidatesOf(resolved.Target, DocumentRegistry.SourceOf(resolved), ElementReader.VersionOf(resolved.Target),
                filter.Groups, out var cached);
            return new Warmed(cached ? 0 : timer.ElapsedMs, !cached);
        }, cancellationToken);
    }

    private async Task<bool> WarmKeyAsync(
        DocumentKey document,
        FilterDefinition filter,
        ICollection<ElementId> categories,
        ParameterKey key,
        bool needsPresence,
        CancellationToken cancellationToken)
    {
        var located = await LocateAsync(document, filter, categories, key, cancellationToken).ConfigureAwait(false);
        var parameters = located.Done
            ? located.Parameters
            : await CompleteAsync(document, filter, categories, key, located, cancellationToken).ConfigureAwait(false);
        if (needsPresence && parameters.Any(parameter => !parameter.SilentChecked))
        {
            parameters = await SilentAsync(document, key, filter.Groups, categories, located, parameters, cancellationToken)
                .ConfigureAwait(false);
        }

        return parameters.Any(parameter => !parameter.Types.IsEmpty || parameter.Silent.Count > 0);
    }

    private Task<Located> LocateAsync(
        DocumentKey document,
        FilterDefinition filter,
        ICollection<ElementId> categories,
        ParameterKey key,
        CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Filter parameter", application =>
        {
            var resolved = DocumentRegistry.Require(application, document);
            var source = DocumentRegistry.SourceOf(resolved);
            var version = ElementReader.VersionOf(resolved.Target);
            return cache.FindResolved(source, key, filter.Groups, version) is { } cached
                ? new Located(source, version, cached, true)
                : new Located(source, version,
                    ParameterLocator.Locate(resolved.Target, key, categories, cache.FindIds(source, version, key)), false);
        }, cancellationToken);
    }

    private async Task<IReadOnlyList<ResolvedParameter>> CompleteAsync(
        DocumentKey document,
        FilterDefinition filter,
        ICollection<ElementId> categories,
        ParameterKey key,
        Located located,
        CancellationToken cancellationToken)
    {
        var typedParameters = await RevitProfiler.RunAsync("Filter parameter types", application =>
                ParameterLocator.WithTypes(DocumentRegistry.Require(application, document).Target, key, categories,
                    located.Parameters),
            cancellationToken).ConfigureAwait(false);
        return await RevitProfiler.RunAsync("Filter parameter lacking", application =>
        {
            var all = ParameterLocator.WithLacking(DocumentRegistry.Require(application, document).Target, key,
                categories, typedParameters);
            cache.StoreResolved(located.Source, key, filter.Groups, located.Version, all);
            return all;
        }, cancellationToken).ConfigureAwait(false);
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
        var lazy = new LazyCandidates(this, resolved.Target, source, version, filter.Groups, timer);
        var compiler = new FilterCompiler(source.Label,
            key => Resolve(resolved.Target, source, version, key, filter.Groups, categories),
            key => raw.TryGetValue(key, out var known) ? known : cache.FindValues(source, key, filter.Groups, version)?.RawValues,
            RuleValueParser.For(resolved.Model.GetUnits()),
            new TypeFilters(() => lazy.Get().MembersIn(resolved.Target)));
        var plan = compiler.Plan(filter.Root);
        if (compiler.MissingValues.Count > 0)
        {
            return new Prepared(source, [], null, [.. compiler.MissingValues]);
        }

        var ids = plan.Native is Compiled.MatchNone ? Array.Empty<long>() : lazy.Get().InScope(scope);
        var collected = lazy.Used && (!lazy.Cached || warmed.Collected);
        RevitProfiler.Logger.FilterCompiled(source.Label, compiler.Rules, compiler.Unresolved, ids.Length,
            plan.HasExactPass, Math.Round(timer.ElapsedMs - lazy.ElapsedMs, 1),
            Math.Round(lazy.ElapsedMs + warmed.CandidatesMs, 1), CandidateOrigin(lazy.Used, collected));
        return new Prepared(source, ids, plan, []);
    }

    private static string CandidateOrigin(bool used, bool collected)
    {
        if (!used)
        {
            return "skipped";
        }

        return collected ? "collected" : "cached";
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

    private sealed class LazyCandidates(
        RevitFilterEvaluator owner,
        Document target,
        SourceId source,
        DocumentVersion? version,
        IReadOnlyCollection<long> groups,
        TraceTimer timer)
    {
        private CandidateSet? _set;

        public bool Used => _set is not null;

        public bool Cached { get; private set; }

        public double ElapsedMs { get; private set; }

        public CandidateSet Get()
        {
            if (_set is null)
            {
                var started = timer.ElapsedMs;
                _set = owner.CandidatesOf(target, source, version, groups, out var cached);
                Cached = cached;
                ElapsedMs += timer.ElapsedMs - started;
            }

            return _set;
        }
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
