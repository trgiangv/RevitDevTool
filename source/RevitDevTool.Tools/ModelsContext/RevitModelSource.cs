using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
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
        return RevitCall.RunAsync<IReadOnlyList<DocumentInfo>>(application =>
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
        var (ids, stamp) = await RevitCall.RunAsync(application =>
        {
            var document = DocumentRegistry.RequireSource(application, source);
            return (ElementReader.CollectIds(document), ElementReader.VersionOf(document));
        }, cancellationToken).ConfigureAwait(false);
        progress?.Report(new SnapshotProgress(source, 0, ids.Length));
        var records = await ReadAsync(application => DocumentRegistry.RequireSource(application, source), ids,
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
        return RevitCall.RunAsync<IReadOnlyCollection<long>>(application => scope switch
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
        return RevitCall.RunAsync<IReadOnlyList<InstanceName>>(application =>
        {
            var target = DocumentRegistry.Require(application, document).Target;
            var names = new List<InstanceName>(ids.Count);
            foreach (var id in ids)
            {
                var element = target.GetElement(RevitIds.From(id));
                var name = element is null
                    ? string.Empty
                    : string.IsNullOrEmpty(element.Name) ? element.Category?.Name ?? string.Empty : element.Name;
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
            return await RevitCall.RunAsync<IReadOnlyList<GroupCount>>(application =>
            {
                var target = DocumentRegistry.Require(application, document).Target;
                return SelectedIds(application, document)
                    .Select(id => target.GetElement(RevitIds.From(id))?.Category)
                    .Where(category => category is not null)
                    .GroupBy(category => RevitIds.ValueOf(category!.Id))
                    .Select(group => new GroupCount(group.Key, group.First()!.Name, group.Count()))
                    .ToList();
            }, cancellationToken).ConfigureAwait(false);
        }

        var categories = await RevitCall.RunAsync(application =>
                RevitIds.ValuesOf(ElementReader.ModelCategoryIds(DocumentRegistry.Require(application, document).Target)),
            cancellationToken).ConfigureAwait(false);
        var counts = new List<GroupCount>();
        var index = 0;
        while (index < categories.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = index;
            var (next, slice) = await RevitCall.RunAsync(application =>
                CountSlice(application, document, scope, categories, start), cancellationToken).ConfigureAwait(false);
            counts.AddRange(slice);
            index = next;
        }

        return counts;
    }

    public Task<DocumentVersion?> GetVersionAsync(SourceId source, CancellationToken cancellationToken)
    {
        return RevitCall.RunAsync(application => DocumentRegistry.ResolveSource(application, source) is { } document
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

        var changes = await RevitCall.RunAsync(application =>
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

                return new ChangedIds(RevitIds.ValuesOf(difference.GetCreatedElementIds()),
                    RevitIds.ValuesOf(difference.GetModifiedElementIds()),
                    RevitIds.ValuesOf(difference.GetDeletedElementIds()));
            }
            catch (Exception)
            {
                return null;
            }
        }, cancellationToken).ConfigureAwait(false);
        if (changes is null)
        {
            return null;
        }

        Document Resolve(UIApplication application) => DocumentRegistry.RequireSource(application, source);
        var added = await ReadAsync(Resolve, changes.Created, null, cancellationToken).ConfigureAwait(false);
        var modified = await ReadAsync(Resolve, changes.Modified, null, cancellationToken).ConfigureAwait(false);
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
        var categories = await RevitCall.RunAsync(application =>
        {
            var document = DocumentRegistry.RequireSource(application, source);
            var filterable = new HashSet<long>(RevitIds.ValuesOf(ParameterFilterUtilities.GetAllFilterableCategories()));
            return RevitIds.ValuesOf(ElementReader.ModelCategoryIds(document)).Where(filterable.Contains).ToList();
        }, cancellationToken).ConfigureAwait(false);
        var seen = new HashSet<long>();
        var catalog = new List<ParameterInfo>();
        for (var start = 0; start < categories.Count; start += CategoriesPerCatalogSlice)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = categories.Skip(start).Take(CategoriesPerCatalogSlice).ToList();
            var slice = await RevitCall.RunAsync(application =>
            {
                var document = DocumentRegistry.RequireSource(application, source);
                var found = new List<ParameterInfo>();
                foreach (var category in chunk)
                {
                    ICollection<ElementId> parameters;
                    try
                    {
                        parameters = ParameterFilterUtilities.GetFilterableParametersInCommon(document,
                            [RevitIds.From(category)]);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    foreach (var parameter in parameters)
                    {
                        if (seen.Add(RevitIds.ValueOf(parameter)) && ParameterResolver.InfoOf(document, parameter) is { } info)
                        {
                            found.Add(info);
                        }
                    }
                }

                return found;
            }, cancellationToken).ConfigureAwait(false);
            catalog.AddRange(slice);
        }

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
        return RevitCall.RunAsync<IReadOnlyDictionary<long, string>>(application =>
        {
            var target = DocumentRegistry.Require(application, document).Target;
            var uniqueIds = new Dictionary<long, string>();
            foreach (var id in ids.Distinct())
            {
                if (target.GetElement(RevitIds.From(id)) is { } element)
                {
                    uniqueIds[id] = element.UniqueId;
                }
            }

            return uniqueIds;
        }, cancellationToken);
    }

    internal static async Task<ReadRecords> ReadAsync(
        Func<UIApplication, Document> resolve,
        IReadOnlyList<long> ids,
        Action<int>? progress,
        CancellationToken cancellationToken)
    {
        var knownTypes = new HashSet<long>();
        var records = new ReadRecords(new List<TypeRecord>(), new List<ElementRecord>(ids.Count));
        var index = 0;
        while (index < ids.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = index;
            var (next, types, elements) = await RevitCall.RunAsync(application =>
            {
                var reader = new ElementReader(resolve(application), knownTypes);
                var end = reader.ReadSlice(ids, start);
                return (end, reader.Types, reader.Elements);
            }, cancellationToken).ConfigureAwait(false);
            records.Types.AddRange(types);
            records.Elements.AddRange(elements);
            index = next;
            progress?.Invoke(index);
        }

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
                ids.Add(RevitIds.ValueOf(reference.ElementId));
            }
            else if (!document.IsCurrentModel && isLinked
                                               && RevitIds.ValueOf(reference.ElementId) == document.LinkInstanceId)
            {
                ids.Add(RevitIds.ValueOf(reference.LinkedElementId));
            }
        }

        if (document.IsCurrentModel)
        {
            ids.UnionWith(RevitIds.ValuesOf(active.Selection.GetElementIds()));
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
        var viewId = RevitIds.ValueOf(view.Id);
        if (scopes.Find(document, viewId) is { } cached)
        {
            return cached;
        }

        HashSet<long> ids;
        try
        {
            ids = [.. RevitIds.ValuesOf(ViewCollector(resolved, view).WhereElementIsNotElementType().ToElementIds())];
        }
        catch (Exception)
        {
            ids = [];
        }

        scopes.Store(document, viewId, ids);
        return ids;
    }

    private static FilteredElementCollector ViewCollector(ResolvedDocument resolved, View view)
    {
        return resolved.Link is { } link
            ? new FilteredElementCollector(resolved.Model, view.Id, link.Id)
            : new FilteredElementCollector(resolved.Model, view.Id);
    }

    private static (int Next, List<GroupCount> Counts) CountSlice(
        UIApplication application,
        DocumentKey document,
        ScopeKind scope,
        IReadOnlyList<long> categories,
        int start)
    {
        var resolved = DocumentRegistry.Require(application, document);
        var view = scope == ScopeKind.ActiveView && DocumentRegistry.IsActive(application, document)
            ? application.ActiveUIDocument?.ActiveView
            : null;
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
            var categoryId = RevitIds.From(categories[index]);
            index++;
            int count;
            try
            {
                count = view is null
                    ? new FilteredElementCollector(resolved.Target)
                        .WherePasses(ElementReader.ModelFilter(resolved.Target, [categoryId], options))
                        .GetElementCount()
                    : ViewCollector(resolved, view)
                        .OfCategoryId(categoryId)
                        .WhereElementIsNotElementType()
                        .GetElementCount();
            }
            catch (Exception)
            {
                continue;
            }

            if (count > 0 && Category.GetCategory(resolved.Target, categoryId) is { } category)
            {
                counts.Add(new GroupCount(RevitIds.ValueOf(categoryId), category.Name, count));
            }
        }

        return (index, counts);
    }

    internal sealed record ReadRecords(List<TypeRecord> Types, List<ElementRecord> Elements);

    private sealed record ChangedIds(List<long> Created, List<long> Modified, List<long> Deleted);
}
