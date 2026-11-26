using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Parameters;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Pane;

namespace RevitDevTool.Tools.ModelsContext.Parameters;

internal static class ParameterCatalogReader
{
    private const int CategoriesPerSlice = 20;

    public static readonly HashSet<long> Unfilterable = [(long)BuiltInParameter.REBAR_ELEM_LENGTH];

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
            var chunk = categories.GetRange(start, Math.Min(CategoriesPerSlice, categories.Count - start));
            await RevitProfiler.RunAsync("Catalog slice", application =>
                    facts.AddFilterable(DocumentRegistry.RequireSource(application, source), chunk, failures),
                cancellationToken, stats).ConfigureAwait(false);
        }

        stats?.Flush(source, categories.Count);
        var ids = await RevitProfiler.RunAsync("Catalog sample ids", application =>
                ElementReader.CollectAnyOption(DocumentRegistry.RequireSource(application, source)),
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
        return ElementReader.ListedCategoryIds(document).Select(id => id.ToLong()).Where(filterable.Contains).ToList();
    }
}
