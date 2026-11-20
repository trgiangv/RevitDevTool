using DevTools.ModelsContext.Model;

namespace RevitDevTool.ModelsContext.LiveTests;

internal static class Picks
{
    public static NumberRange? Pick(Adapter adapter, IReadOnlyList<SampleDocument> documents, ParameterKey key, IReadOnlyList<long> groups, string text)
    {
        NumberRange? picked = null;
        foreach (var source in documents.Select(document => document.Source).Distinct())
        {
            var counts = Adapter.Run(adapter.Evaluator.CountValuesAsync(source, key, groups, CancellationToken.None));
            if (counts.Numbers is { } numbers && numbers.TryGetValue(text, out var range))
            {
                picked = picked is { } known ? known.Union(range) : range;
            }
        }

        if (picked is { } found)
        {
            Evidence.Step($"Picked '{text}' from the value list of '{key.Name}': stored {found.Min:R} to {found.Max:R}.");
        }

        return picked;
    }

    public static IReadOnlyList<ParameterInfo> Catalog(Adapter adapter, SampleDocument document)
    {
        return Adapter.Run(adapter.Source.GetParameterCatalogAsync(document.Source, CancellationToken.None));
    }
}
