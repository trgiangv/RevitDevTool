using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Scope;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Pane;
using System.Diagnostics;

namespace RevitDevTool.Tools.ModelsContext.Documents;

internal static class ScopeGroupCounter
{
    public static async Task<IReadOnlyList<GroupCount>> CountAsync(
        DocumentKey document,
        ScopeKind scope,
        CancellationToken cancellationToken)
    {
        if (scope == ScopeKind.Selection)
        {
            return await RevitProfiler.RunAsync<IReadOnlyList<GroupCount>>("Selection counts",
                application => SelectionCounts(application, document), cancellationToken).ConfigureAwait(false);
        }

        // One collector per category walks the whole document each time (~100x slower on the sample).
        var ids = await RevitProfiler.RunAsync("Count ids", application => CountIds(application, document, scope),
            cancellationToken).ConfigureAwait(false);
        var counts = new Dictionary<long, (string Name, int Count, long Parent)>();
        var stats = ProfileStats.Start("Count");
        var failures = FailureTally.Start(RevitProfiler.Logger);
        var index = 0;
        while (index < ids.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = index;
            index = await RevitProfiler.RunAsync("Count slice", application =>
                    CountSlice(application, document, ids, start, counts, failures), cancellationToken, stats)
                .ConfigureAwait(false);
        }

        failures?.Flush("Count", document, ids.Length);
        stats?.Flush(document, ids.Length);
        return counts.Select(pair => new GroupCount(pair.Key, pair.Value.Name, pair.Value.Count, pair.Value.Parent)).ToList();
    }

    private static IReadOnlyList<GroupCount> SelectionCounts(UIApplication application, DocumentKey document)
    {
        var target = DocumentRegistry.Require(application, document).Target;
        return RevitModelSource.SelectedIds(application, document)
            .Select(id => target.GetElement(id.ToElementId())?.Category)
            .Where(category => category is not null)
            .GroupBy(category => category!.Id.ToLong())
            .Select(group => new GroupCount(group.Key, group.First()!.Name, group.Count(),
                ElementReader.ParentOf(group.First()!)))
            .ToList();
    }

    private static View? ActiveViewOf(UIApplication application, DocumentKey document)
    {
        return DocumentRegistry.IsActive(application, document) ? application.ActiveUIDocument?.ActiveView : null;
    }

    private static long[] CountIds(UIApplication application, DocumentKey document, ScopeKind scope)
    {
        var resolved = DocumentRegistry.Require(application, document);
        if (scope != ScopeKind.ActiveView)
        {
            return ElementReader.CollectIds(resolved.Target);
        }

        if (ActiveViewOf(application, document) is not { } view || RevitModelSource.ViewCollector(resolved, view) is not { } collector)
        {
            return [];
        }

        var drawn = collector.WherePasses(ElementReader.AnyOptionFilter(resolved.Target)).ToElementIds();
        return drawn.Select(id => id.ToLong())
            .Union(RevitModelSource.PartsDrawnWith(resolved.Target, view, drawn, document))
            .ToArray();
    }

    private static int CountSlice(
        UIApplication application,
        DocumentKey document,
        long[] ids,
        int start,
        Dictionary<long, (string Name, int Count, long Parent)> counts,
        FailureTally? failures)
    {
        var target = DocumentRegistry.Require(application, document).Target;
        var watch = Stopwatch.StartNew();
        var index = start;
        while (index < ids.Length && (index == start || watch.Elapsed < ElementReader.SliceTime))
        {
            CountOne(target, ids[index], counts, failures);
            index++;
        }

        return index;
    }

    private static void CountOne(
        Document target,
        long id,
        Dictionary<long, (string Name, int Count, long Parent)> counts,
        FailureTally? failures)
    {
        try
        {
            if (target.GetElement(id.ToElementId())?.Category is not { } category)
            {
                return;
            }

            var key = category.Id.ToLong();
            counts[key] = counts.TryGetValue(key, out var known)
                ? known with { Count = known.Count + 1 }
                : (category.Name, 1, ElementReader.ParentOf(category));
        }
        catch (Exception exception)
        {
            failures?.Add(exception);
        }
    }
}
