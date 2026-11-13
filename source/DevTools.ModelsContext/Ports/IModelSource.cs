using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Ports;

public interface IModelSource
{
    string HostName { get; }

    TreeSchema Schema { get; }

    Task<IReadOnlyList<DocumentInfo>> GetDocumentsAsync(CancellationToken cancellationToken);

    Task<DocumentSnapshot> BuildSnapshotAsync(
        SourceId source,
        IProgress<SnapshotProgress>? progress,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<long>> CollectScopeAsync(
        DocumentKey document,
        ScopeKind scope,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InstanceName>> GetInstanceNamesAsync(
        DocumentKey document,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GroupCount>> CountGroupsAsync(
        DocumentKey document,
        ScopeKind scope,
        CancellationToken cancellationToken);

    Task<DocumentVersion?> GetVersionAsync(SourceId source, CancellationToken cancellationToken);

    Task<ElementDelta?> GetChangesSinceAsync(
        SourceId source,
        DocumentVersion since,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ParameterInfo>> GetParameterCatalogAsync(
        SourceId source,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<long, string>> GetUniqueIdsAsync(
        DocumentKey document,
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken);
}
