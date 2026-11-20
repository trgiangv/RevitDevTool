using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using Nice3point.Revit.Extensions;
using System.Diagnostics;
using System.Globalization;
using DocumentVersion = DevTools.ModelsContext.Model.DocumentVersion;
using ElementRecord = DevTools.ModelsContext.Model.ElementRecord;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class RevitModelSource(ScopeCache scopes, ParameterCache parameters) : IModelSource
{
    public const string Revit = "Revit";

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

            foreach (var instance in model.CollectElements()
                         .OfClass<RevitLinkInstance>()
                         .Cast<RevitLinkInstance>())
            {
                var loaded = instance.GetLinkDocument() is not null;
                documents.Add(new DocumentInfo(DocumentRegistry.KeyOf(model, instance),
                    DocumentRegistry.LinkSourceOf(instance), DocumentRegistry.LinkNameOf(instance),
                    loaded, LinkCapabilities(loaded)));
            }

            return documents;
        }, cancellationToken);
    }

    private static DocumentCapabilities LinkCapabilities(bool loaded)
    {
        if (!loaded)
        {
            return DocumentCapabilities.WholeModel;
        }

        return LinkedHideShow.IsAvailable
            ? DocumentCapabilities.WholeModel | DocumentCapabilities.ReferenceCommands | DocumentCapabilities.ReferenceUnhide
            : DocumentCapabilities.WholeModel;
    }

    public async Task<DocumentSnapshot> BuildSnapshotAsync(
        SourceId source,
        IProgress<SnapshotProgress>? progress,
        CancellationToken cancellationToken)
    {
        var (ids, stamp) = await RevitProfiler.RunAsync("Snapshot ids", application =>
        {
            var document = DocumentRegistry.RequireSource(application, source);
            return (ElementReader.CollectAnyOption(document), ElementReader.VersionOf(document));
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

        // One collector per category walks the whole document each time (~100x slower on the sample).
        var ids = await RevitProfiler.RunAsync("Count ids", application => CountIds(application, document, scope),
            cancellationToken).ConfigureAwait(false);
        var counts = new Dictionary<long, (string Name, int Count)>();
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
        return counts.Select(pair => new GroupCount(pair.Key, pair.Value.Name, pair.Value.Count)).ToList();
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
                    RevitProfiler.Logger.ChangesUnavailable(source.ToString());
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

    public Task<IReadOnlyList<ParameterInfo>> GetParameterCatalogAsync(
        SourceId source,
        CancellationToken cancellationToken)
    {
        return ParameterCatalogReader.ReadAsync(source, parameters, cancellationToken);
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

        ViewActivations.ThrowIfChangedInCall();
        HashSet<long> ids;
        try
        {
            ids = ViewCollector(resolved, view) is { } collector
                ? [.. collector.WhereElementIsNotElementType().ToElementIds().Select(id => id.ToLong())]
                : [];
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

    private static FilteredElementCollector? ViewCollector(ResolvedDocument resolved, View view)
    {
        ViewActivations.ThrowIfChangedInCall();
        return resolved.Link is { } link
            ? LinkElements.InView(resolved.Model, view, link.Id)
            : resolved.Model.CollectElements(view.Id);
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

        return ActiveViewOf(application, document) is { } view && ViewCollector(resolved, view) is { } collector
            ? collector.WherePasses(ElementReader.AnyOptionFilter(resolved.Target)).ToElementIds().Select(id => id.ToLong()).ToArray()
            : [];
    }

    private static int CountSlice(
        UIApplication application,
        DocumentKey document,
        long[] ids,
        int start,
        Dictionary<long, (string Name, int Count)> counts,
        FailureTally? failures)
    {
        var target = DocumentRegistry.Require(application, document).Target;
        var watch = Stopwatch.StartNew();
        var index = start;
        while (index < ids.Length && (index == start || watch.Elapsed < ElementReader.SliceTime))
        {
            var id = ids[index];
            index++;
            try
            {
                if (target.GetElement(id.ToElementId())?.Category is not { } category)
                {
                    continue;
                }

                var key = category.Id.ToLong();
                counts[key] = counts.TryGetValue(key, out var known) ? (known.Name, known.Count + 1) : (category.Name, 1);
            }
            catch (Exception exception)
            {
                failures?.Add(exception);
            }
        }

        return index;
    }

    internal sealed record ReadRecords(List<TypeRecord> Types, List<ElementRecord> Elements);

    private sealed record ChangedIds(List<long> Created, List<long> Modified, List<long> Deleted);
}
