using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using Nice3point.Revit.Extensions;
using System.Diagnostics;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class RevitFilterEvaluator : IFilterEvaluator
{
    public async Task<IReadOnlyCollection<long>> EvaluateAsync(
        DocumentKey document,
        FilterDefinition filter,
        IReadOnlyCollection<long>? scope,
        CancellationToken cancellationToken)
    {
        var candidates = await RevitProfiler.RunAsync("Filter candidates", application =>
        {
            var target = DocumentRegistry.Require(application, document).Target;
            return InScope(ElementReader.CollectIds(target, Categories(filter.Groups)), scope);
        }, cancellationToken).ConfigureAwait(false);
        return await MatchInSlicesAsync(document, candidates, (target, chunk) => Match(target, chunk, filter),
            cancellationToken).ConfigureAwait(false);
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
        var candidates = await RevitProfiler.RunAsync("Value candidates", application =>
                ElementReader.CollectIds(DocumentRegistry.RequireSource(application, source), Categories(groups)),
            cancellationToken).ConfigureAwait(false);
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        var stats = ProfileStats.Start("Values");
        var index = 0;
        while (index < candidates.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = index;
            var (next, found) = await RevitProfiler.RunAsync("Value slice", application =>
                    ValueTextSlice(DocumentRegistry.RequireSource(application, source), candidates, start, parameter),
                cancellationToken, stats).ConfigureAwait(false);
            Tally(values, found);
            index = next;
        }

        stats?.Flush(source, candidates.Length);
        return new ParameterValueCounts(candidates.Length, values);
    }

    private static (int Next, List<string> Texts) ValueTextSlice(
        Document document,
        long[] candidates,
        int start,
        ParameterKey parameter)
    {
        var texts = new List<string>();
        var watch = Stopwatch.StartNew();
        var position = start;
        while (position < candidates.Length && position - start < ElementReader.SliceSize
                                             && watch.Elapsed < ElementReader.SliceTime)
        {
            if (ValueTextOf(document, candidates[position], parameter) is { } text)
            {
                texts.Add(text);
            }

            position++;
        }

        return (position, texts);
    }

    private static string? ValueTextOf(Document document, long id, ParameterKey parameter)
    {
        return document.GetElement(id.ToElementId()) is { } element
               && ParameterResolver.Find(element, parameter) is { } value
            ? ParameterResolver.ValueText(document, value)
            : null;
    }

    private static void Tally(Dictionary<string, int> values, List<string> texts)
    {
        foreach (var text in texts)
        {
            values[text] = values.TryGetValue(text, out var count) ? count + 1 : 1;
        }
    }

    private static List<long> Match(Document target, List<ElementId> chunk, FilterDefinition filter)
    {
        if (chunk.Count == 0)
        {
            return [];
        }

        return new FilterCompiler(target, Categories(filter.Groups)).Compile(filter.Root) switch
        {
            Compiled.MatchNone => [],
            Compiled.Filter matcher => new FilteredElementCollector(target, chunk)
                .WherePasses(matcher.Value)
                .ToElementIds()
                .Select(id => id.ToLong())
                .ToList(),
            _ => chunk.Select(id => id.ToLong()).ToList()
        };
    }

    private static List<ElementId> Categories(IEnumerable<long> groups)
    {
        return groups.Select(id => id.ToElementId()).ToList();
    }
}
