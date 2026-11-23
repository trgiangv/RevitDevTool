using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Pane;
using System.Diagnostics;

namespace RevitDevTool.Tools.ModelsContext.Filters;

internal static class SliceMatcher
{
    public const int MatchBatch = 500;
    public const int FirstBatch = 100;
    public const int MinBatch = 25;

    public static async Task<IReadOnlyCollection<long>> MatchInSlicesAsync(
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
            var chunk = LiveIds(target, candidates, position, end);

            if (chunk.Count > 0)
            {
                matches.AddRange(match(target, chunk));
            }

            pace.Record(end - position, watch.Elapsed - batchStarted);
            position = end;
        }

        return (position, matches);
    }

    private static List<ElementId> LiveIds(Document target, long[] candidates, int start, int end)
    {
        var chunk = new List<ElementId>(end - start);
        for (var i = start; i < end; i++)
        {
            var id = candidates[i].ToElementId();
            if (target.GetElement(id) is not null)
            {
                chunk.Add(id);
            }
        }

        return chunk;
    }
}
