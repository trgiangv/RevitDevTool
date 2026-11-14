using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
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
        var candidates = await RevitCall.RunAsync(application =>
        {
            var target = DocumentRegistry.Require(application, document).Target;
            var ids = ElementReader.CollectIds(target, Categories(filter.Groups));
            if (scope is null)
            {
                return ids;
            }

            var inScope = scope as ISet<long> ?? new HashSet<long>(scope);
            return ids.Where(inScope.Contains).ToArray();
        }, cancellationToken).ConfigureAwait(false);
        var result = new List<long>();
        var index = 0;
        while (index < candidates.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = index;
            var (next, matches) = await RevitCall.RunAsync(application =>
            {
                var target = DocumentRegistry.Require(application, document).Target;
                var end = Math.Min(candidates.Length, start + ElementReader.SliceSize);
                var chunk = RevitIds.From(candidates.Skip(start).Take(end - start))
                    .Where(id => target.GetElement(id) is not null)
                    .ToList();
                return (end, Match(target, chunk, filter));
            }, cancellationToken).ConfigureAwait(false);
            result.AddRange(matches);
            index = next;
        }

        return result;
    }

    public async Task<ParameterValueCounts> CountValuesAsync(
        SourceId source,
        ParameterKey parameter,
        IReadOnlyCollection<long> groups,
        CancellationToken cancellationToken)
    {
        var candidates = await RevitCall.RunAsync(application =>
                ElementReader.CollectIds(DocumentRegistry.RequireSource(application, source), Categories(groups)),
            cancellationToken).ConfigureAwait(false);
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        var index = 0;
        while (index < candidates.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = index;
            var (next, found) = await RevitCall.RunAsync(application =>
            {
                var document = DocumentRegistry.RequireSource(application, source);
                var texts = new List<string>();
                var watch = Stopwatch.StartNew();
                var position = start;
                while (position < candidates.Length && position - start < ElementReader.SliceSize
                                                     && watch.Elapsed < ElementReader.SliceTime)
                {
                    if (document.GetElement(RevitIds.From(candidates[position])) is { } element
                        && ParameterResolver.Find(element, parameter) is { } value
                        && ParameterResolver.ValueText(document, value) is { } text)
                    {
                        texts.Add(text);
                    }

                    position++;
                }

                return (position, texts);
            }, cancellationToken).ConfigureAwait(false);
            foreach (var text in found)
            {
                values[text] = values.TryGetValue(text, out var count) ? count + 1 : 1;
            }

            index = next;
        }

        return new ParameterValueCounts(candidates.Length, values);
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
            Compiled.Filter matcher => RevitIds.ValuesOf(new FilteredElementCollector(target, chunk)
                .WherePasses(matcher.Value)
                .ToElementIds()),
            _ => RevitIds.ValuesOf(chunk)
        };
    }

    private static List<ElementId> Categories(IEnumerable<long> groups)
    {
        return RevitIds.From(groups);
    }
}
