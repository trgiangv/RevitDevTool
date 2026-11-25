using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Scope;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Pane;
using RevitDevTool.Tools.ModelsContext.Parameters;
using RevitDevTool.Tools.ModelsContext.Visibility;
using System.Diagnostics;
using System.Globalization;
using DocumentVersion = DevTools.ModelsContext.Documents.DocumentVersion;
using ElementRecord = DevTools.ModelsContext.Documents.ElementRecord;

namespace RevitDevTool.Tools.ModelsContext.Documents;

internal sealed class RevitModelSource(ScopeCache scopes, ParameterCache parameters) : IModelSource
{
    public const string Revit = "Revit";

    public string HostName => Revit;

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
                    DocumentCapabilities.Elements | DocumentCapabilities.Categories)
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

    public Task<IReadOnlyCollection<DocumentKey>> GetViewLinksAsync(CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync<IReadOnlyCollection<DocumentKey>>("View links", application =>
        {
            if (application.ActiveUIDocument is not { Document: { IsFamilyDocument: false } model, ActiveView: { } view }
                || view.IsTemplate)
            {
                return [];
            }

            ViewActivations.ThrowIfChangedInCall();
            return model.CollectElements(view.Id)
                .OfClass<RevitLinkInstance>()
                .Cast<RevitLinkInstance>()
                .Where(instance => instance.GetLinkDocument() is not null)
                .Select(instance => DocumentRegistry.KeyOf(model, instance))
                .ToList();
        }, cancellationToken);
    }

    private static DocumentCapabilities LinkCapabilities(bool loaded)
    {
        if (!loaded)
        {
            return DocumentCapabilities.LinkInstance;
        }

        var capabilities = LinkViewFilters.IsSupported
            ? DocumentCapabilities.LinkInstance | DocumentCapabilities.LinkFilters
            : DocumentCapabilities.LinkInstance;
        return LinkHideShow.IsAvailable
            ? capabilities | DocumentCapabilities.LinkElements | DocumentCapabilities.LinkShow
            : capabilities;
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
        return await Task.Run(() => Assemble(source, records, stamp), cancellationToken).ConfigureAwait(false);
    }

    private static DocumentSnapshot Assemble(SourceId source, ReadRecords records, DocumentVersion? stamp)
    {
        var builder = new DocumentSnapshotBuilder(source, DocumentSnapshot.MaxGroupingDepth);
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

    public Task<IReadOnlyList<GroupCount>> CountGroupsAsync(
        DocumentKey document,
        ScopeKind scope,
        CancellationToken cancellationToken)
    {
        return ScopeGroupCounter.CountAsync(document, scope, cancellationToken);
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
            AddReference(ids, reference, document);
        }

        if (document.IsCurrentModel)
        {
            ids.UnionWith(active.Selection.GetElementIds().Select(id => id.ToLong()));
        }

        return ids;
    }

    private static void AddReference(HashSet<long> ids, Reference reference, DocumentKey document)
    {
        var isLinked = reference.LinkedElementId != ElementId.InvalidElementId;
        if (document.IsCurrentModel)
        {
            if (!isLinked)
            {
                ids.Add(reference.ElementId.ToLong());
            }

            return;
        }

        if (isLinked && reference.ElementId.ToLong() == document.LinkInstanceId)
        {
            ids.Add(reference.LinkedElementId.ToLong());
        }
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
        ICollection<ElementId> drawn;
        try
        {
            drawn = ViewCollector(resolved, view) is { } collector ? collector.WhereElementIsNotElementType().ToElementIds() : [];
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "View ids", document);
            return [];
        }

        var ids = new HashSet<long>(drawn.Select(id => id.ToLong()));
        ids.UnionWith(PartsDrawnWith(resolved.Target, view, drawn, document));
        scopes.Store(document, viewId, ids);
        return ids;
    }

    internal static List<long> PartsDrawnWith(Document target, View view, ICollection<ElementId> drawn, DocumentKey document)
    {
        try
        {
            return RailingParts.DrawnWith(target, view, drawn);
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Railing parts", document);
            return [];
        }
    }

    private static string DisplayNameOf(Element? element)
    {
        if (element is null)
        {
            return string.Empty;
        }

        return string.IsNullOrEmpty(element.Name) ? element.Category?.Name ?? string.Empty : element.Name;
    }

    internal static FilteredElementCollector? ViewCollector(ResolvedDocument resolved, View view)
    {
        ViewActivations.ThrowIfChangedInCall();
        return resolved.Link is { } link
            ? LinkElements.InView(resolved.Model, view, link.Id)
            : resolved.Model.CollectElements(view.Id);
    }

    internal sealed record ReadRecords(List<TypeRecord> Types, List<ElementRecord> Elements);

    private sealed record ChangedIds(List<long> Created, List<long> Modified, List<long> Deleted);
}
