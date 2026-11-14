using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using Nice3point.Revit.Extensions;
using System.Diagnostics;
using System.Globalization;
using DocumentVersion = DevTools.ModelsContext.Model.DocumentVersion;
using ElementRecord = DevTools.ModelsContext.Model.ElementRecord;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class RevitModelSource(ScopeCache scopes) : IModelSource
{
    public const string Revit = "Revit";

    private const int CategoriesPerCatalogSlice = 20;

    private static readonly TreeSchema RevitSchema = new(["Category", "Family", "Type", "Instance"]);

    public string HostName => Revit;

    public TreeSchema Schema => RevitSchema;

    public Task<IReadOnlyList<DocumentInfo>> GetDocumentsAsync(CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync<IReadOnlyList<DocumentInfo>>("Documents", application =>
        {
            if (application.ActiveUIDocument?.Document is not { } model)
            {
                return [];
            }

            var documents = new List<DocumentInfo>
            {
                new(DocumentRegistry.KeyOf(model), DocumentRegistry.SourceOf(model), model.Title, true,
                    DocumentCapabilities.HideById | DocumentCapabilities.HideGroupEverywhere)
            };
            if (model.IsFamilyDocument)
            {
                return documents;
            }

            foreach (var instance in new FilteredElementCollector(model)
                         .OfClass(typeof(RevitLinkInstance))
                         .Cast<RevitLinkInstance>())
            {
                documents.Add(new DocumentInfo(DocumentRegistry.KeyOf(model, instance),
                    DocumentRegistry.LinkSourceOf(instance), DocumentRegistry.LinkNameOf(instance),
                    instance.GetLinkDocument() is not null, DocumentCapabilities.WholeModel));
            }

            return documents;
        }, cancellationToken);
    }

    public async Task<DocumentSnapshot> BuildSnapshotAsync(
        SourceId source,
        IProgress<SnapshotProgress>? progress,
        CancellationToken cancellationToken)
    {
        var (ids, stamp) = await RevitProfiler.RunAsync("Snapshot ids", application =>
        {
            var document = DocumentRegistry.RequireSource(application, source);
            return (ElementReader.CollectIds(document), ElementReader.VersionOf(document));
        }, cancellationToken).ConfigureAwait(false);
        progress?.Report(new SnapshotProgress(source, 0, ids.Length));
        var records = await ReadAsync(source, ids,
            count => progress?.Report(new SnapshotProgress(source, count, ids.Length)), cancellationToken)
            .ConfigureAwait(false);
        return await Task.Run(() =>
        {
            var builder = new DocumentSnapshotBuilder(source, RevitSchema.GroupingDepth);
            foreach (var type in records.Types)
            {
                builder.AddType(type);
            }

            var seen = new HashSet<long>();
            foreach (var element in records.Elements)
            {
                if (seen.Add(element.Id))
                {
                    builder.AddElement(element);
                }
            }

            if (stamp is { } version)
            {
                builder.WithStamp(version);
            }

            return builder.Build();
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyCollection<long>> CollectScopeAsync(
        DocumentKey document,
        ScopeKind scope,
        CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync<IReadOnlyCollection<long>>("Scope ids", application => scope switch
        {
            ScopeKind.Selection => SelectedIds(application, document),
            ScopeKind.ActiveView => ViewIds(application, document),
            _ => new HashSet<long>(ElementReader.CollectIds(DocumentRegistry.Require(application, document).Target))
        }, cancellationToken);
    }

    public Task<IReadOnlyList<InstanceName>> GetInstanceNamesAsync(
        DocumentKey document,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync<IReadOnlyList<InstanceName>>("Instance names", application =>
        {
            var target = DocumentRegistry.Require(application, document).Target;
            var names = new List<InstanceName>(ids.Count);
            foreach (var id in ids)
            {
                var name = DisplayNameOf(target.GetElement(id.ToElementId()));
                names.Add(new InstanceName(id, string.Format(CultureInfo.InvariantCulture, "{0} [{1}]", name, id)));
            }

            return names;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<GroupCount>> CountGroupsAsync(
        DocumentKey document,
        ScopeKind scope,
        CancellationToken cancellationToken)
    {
        if (scope == ScopeKind.Selection)
        {
            return await RevitProfiler.RunAsync<IReadOnlyList<GroupCount>>("Selection counts", application =>
            {
                var target = DocumentRegistry.Require(application, document).Target;
                return SelectedIds(application, document)
                    .Select(id => target.GetElement(id.ToElementId())?.Category)
                    .Where(category => category is not null)
                    .GroupBy(category => category!.Id.ToLong())
                    .Select(group => new GroupCount(group.Key, group.First()!.Name, group.Count()))
                    .ToList();
            }, cancellationToken).ConfigureAwait(false);
        }

        var categories = await RevitProfiler.RunAsync("Count categories", application =>
                ElementReader.ModelCategoryIds(DocumentRegistry.Require(application, document).Target).Select(id => id.ToLong()).ToList(),
            cancellationToken).ConfigureAwait(false);
        var counts = new List<GroupCount>();
        var stats = ProfileStats.Start("Count");
        var failures = FailureTally.Start(RevitProfiler.Logger);
        var index = 0;
        while (index < categories.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = index;
            var (next, slice) = await RevitProfiler.RunAsync("Count slice", application =>
                    CountSlice(application, document, scope, categories, start, failures), cancellationToken, stats)
                .ConfigureAwait(false);
            counts.AddRange(slice);
            index = next;
        }

        failures?.Flush("Count", document, categories.Count);
        stats?.Flush(document, categories.Count);
        return counts;
    }

    public Task<DocumentVersion?> GetVersionAsync(SourceId source, CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Version", application => DocumentRegistry.ResolveSource(application, source) is { } document
            ? ElementReader.VersionOf(document)
            : null, cancellationToken);
    }

    public async Task<ElementDelta?> GetChangesSinceAsync(
        SourceId source,
        DocumentVersion since,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(since.VersionGuid, out var baseVersion))
        {
            return null;
        }

        var changes = await RevitProfiler.RunAsync("Changes", application =>
        {
            if (DocumentRegistry.ResolveSource(application, source) is not { } document)
            {
                return null;
            }

            try
            {
                var difference = document.GetChangedElements(baseVersion);
                if (!difference.AreDeletedElementIdsAvailable)
                {
                    return null;
                }

                return new ChangedIds(difference.GetCreatedElementIds().Select(id => id.ToLong()).ToList(),
                    difference.GetModifiedElementIds().Select(id => id.ToLong()).ToList(),
                    difference.GetDeletedElementIds().Select(id => id.ToLong()).ToList());
            }
            catch (Exception exception)
            {
                RevitProfiler.Logger.Swallowed(exception, "Changes", source);
                return null;
            }
        }, cancellationToken).ConfigureAwait(false);
        if (changes is null)
        {
            return null;
        }

        var added = await ReadAsync(source, changes.Created, null, cancellationToken).ConfigureAwait(false);
        var modified = await ReadAsync(source, changes.Modified, null, cancellationToken).ConfigureAwait(false);
        var types = added.Types.Concat(modified.Types)
            .GroupBy(type => type.TypeId)
            .Select(group => group.Last())
            .ToList();
        return new ElementDelta(source, types, added.Elements, modified.Elements, changes.Deleted);
    }

    public async Task<IReadOnlyList<ParameterInfo>> GetParameterCatalogAsync(
        SourceId source,
        CancellationToken cancellationToken)
    {
        var categories = await RevitProfiler.RunAsync("Catalog categories", application =>
                FilterableCategories(DocumentRegistry.RequireSource(application, source)),
            cancellationToken).ConfigureAwait(false);
        var seen = new HashSet<long>();
        var catalog = new List<ParameterInfo>();
        var stats = ProfileStats.Start("Catalog");
        var failures = FailureTally.Start(RevitProfiler.Logger);
        for (var start = 0; start < categories.Count; start += CategoriesPerCatalogSlice)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = categories.Skip(start).Take(CategoriesPerCatalogSlice).ToList();
            var slice = await RevitProfiler.RunAsync("Catalog slice", application =>
                    CatalogSlice(DocumentRegistry.RequireSource(application, source), chunk, seen, failures),
                cancellationToken, stats).ConfigureAwait(false);
            catalog.AddRange(slice);
        }

        failures?.Flush("Catalog", source, categories.Count);
        stats?.Flush(source, categories.Count);
        return catalog
            .GroupBy(info => info.Key)
            .Select(group => group.First())
            .OrderBy(info => info.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public Task<IReadOnlyDictionary<long, string>> GetUniqueIdsAsync(
        DocumentKey document,
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync<IReadOnlyDictionary<long, string>>("Unique ids", application =>
        {
            var target = DocumentRegistry.Require(application, document).Target;
            var uniqueIds = new Dictionary<long, string>();
            foreach (var id in ids.Distinct())
            {
                if (target.GetElement(id.ToElementId()) is { } element)
                {
                    uniqueIds[id] = element.UniqueId;
                }
            }

            return uniqueIds;
        }, cancellationToken);
    }

    private static async Task<ReadRecords> ReadAsync(
        SourceId source,
        IReadOnlyList<long> ids,
        Action<int>? progress,
        CancellationToken cancellationToken)
    {
        var knownTypes = new HashSet<long>();
        var records = new ReadRecords(new List<TypeRecord>(), new List<ElementRecord>(ids.Count));
        var stats = ProfileStats.Start("Read");
        var index = 0;
        while (index < ids.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = index;
            var (next, types, elements) = await RevitProfiler.RunAsync("Read slice", application =>
            {
                var reader = new ElementReader(DocumentRegistry.RequireSource(application, source), knownTypes);
                var end = reader.ReadSlice(ids, start);
                return (end, reader.Types, reader.Elements);
            }, cancellationToken, stats).ConfigureAwait(false);
            records.Types.AddRange(types);
            records.Elements.AddRange(elements);
            index = next;
            progress?.Invoke(index);
        }

        stats?.Flush(source, ids.Count);
        return records;
    }

    internal static HashSet<long> SelectedIds(UIApplication application, DocumentKey document)
    {
        var ids = new HashSet<long>();
        if (application.ActiveUIDocument is not { } active || !DocumentRegistry.IsActive(application, document))
        {
            return ids;
        }

        foreach (var reference in active.Selection.GetReferences())
        {
            var isLinked = reference.LinkedElementId != ElementId.InvalidElementId;
            if (document.IsCurrentModel && !isLinked)
            {
                ids.Add(reference.ElementId.ToLong());
            }
            else if (!document.IsCurrentModel && isLinked
                                               && reference.ElementId.ToLong() == document.LinkInstanceId)
            {
                ids.Add(reference.LinkedElementId.ToLong());
            }
        }

        if (document.IsCurrentModel)
        {
            ids.UnionWith(active.Selection.GetElementIds().Select(id => id.ToLong()));
        }

        return ids;
    }

    private HashSet<long> ViewIds(UIApplication application, DocumentKey document)
    {
        if (application.ActiveUIDocument?.ActiveView is not { } view || !DocumentRegistry.IsActive(application, document))
        {
            return [];
        }

        var resolved = DocumentRegistry.Require(application, document);
        var viewId = view.Id.ToLong();
        if (scopes.Find(document, viewId) is { } cached)
        {
            return cached;
        }

        HashSet<long> ids;
        try
        {
            ids = [.. ViewCollector(resolved, view).WhereElementIsNotElementType().ToElementIds().Select(id => id.ToLong())];
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "View ids", document);
            ids = [];
        }

        scopes.Store(document, viewId, ids);
        return ids;
    }

    private static string DisplayNameOf(Element? element)
    {
        if (element is null)
        {
            return string.Empty;
        }

        return string.IsNullOrEmpty(element.Name) ? element.Category?.Name ?? string.Empty : element.Name;
    }

    private static FilteredElementCollector ViewCollector(ResolvedDocument resolved, View view)
    {
        return resolved.Link is { } link
            ? new FilteredElementCollector(resolved.Model, view.Id, link.Id)
            : new FilteredElementCollector(resolved.Model, view.Id);
    }

    private static List<long> FilterableCategories(Document document)
    {
        var filterable = new HashSet<long>(ParameterFilterUtilities.GetAllFilterableCategories().Select(id => id.ToLong()));
        return ElementReader.ModelCategoryIds(document).Select(id => id.ToLong()).Where(filterable.Contains).ToList();
    }

    private static List<ParameterInfo> CatalogSlice(
        Document document,
        List<long> categories,
        HashSet<long> seen,
        FailureTally? failures)
    {
        var found = new List<ParameterInfo>();
        foreach (var category in categories)
        {
            foreach (var parameter in FilterableParameters(document, category, failures))
            {
                if (seen.Add(parameter.ToLong()) && ParameterResolver.InfoOf(document, parameter, failures) is { } info)
                {
                    found.Add(info);
                }
            }
        }

        return found;
    }

    private static ICollection<ElementId> FilterableParameters(Document document, long category, FailureTally? failures)
    {
        try
        {
            return ParameterFilterUtilities.GetFilterableParametersInCommon(document, [category.ToElementId()]);
        }
        catch (Exception exception)
        {
            failures?.Add(exception);
            return [];
        }
    }

    private static (int Next, List<GroupCount> Counts) CountSlice(
        UIApplication application,
        DocumentKey document,
        ScopeKind scope,
        IReadOnlyList<long> categories,
        int start,
        FailureTally? failures)
    {
        var resolved = DocumentRegistry.Require(application, document);
        var view = scope == ScopeKind.ActiveView ? ActiveViewOf(application, document) : null;
        if (scope == ScopeKind.ActiveView && view is null)
        {
            return (categories.Count, []);
        }

        var options = ElementReader.OptionFilters(resolved.Target);
        var counts = new List<GroupCount>();
        var watch = Stopwatch.StartNew();
        var index = start;
        while (index < categories.Count && (index == start || watch.Elapsed < ElementReader.SliceTime))
        {
            var group = categories[index];
            var categoryId = group.ToElementId();
            index++;
            if (CountOf(resolved, view, categoryId, options, failures) is int count and > 0
                && Category.GetCategory(resolved.Target, categoryId) is { } category)
            {
                counts.Add(new GroupCount(group, category.Name, count));
            }
        }

        return (index, counts);
    }

    private static View? ActiveViewOf(UIApplication application, DocumentKey document)
    {
        return DocumentRegistry.IsActive(application, document) ? application.ActiveUIDocument?.ActiveView : null;
    }

    private static int? CountOf(
        ResolvedDocument resolved,
        View? view,
        ElementId categoryId,
        IReadOnlyList<ElementFilter> options,
        FailureTally? failures)
    {
        try
        {
            return view is null
                ? new FilteredElementCollector(resolved.Target)
                    .WherePasses(ElementReader.ModelFilter(resolved.Target, [categoryId], options))
                    .GetElementCount()
                : ViewCollector(resolved, view)
                    .OfCategoryId(categoryId)
                    .WhereElementIsNotElementType()
                    .GetElementCount();
        }
        catch (Exception exception)
        {
            failures?.Add(exception);
            return null;
        }
    }

    internal sealed record ReadRecords(List<TypeRecord> Types, List<ElementRecord> Elements);

    private sealed record ChangedIds(List<long> Created, List<long> Modified, List<long> Deleted);
}
