using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Scope;

namespace DevTools.ModelsContext.Documents;

public interface IModelSource
{
    string HostName { get; }

    Task<IReadOnlyList<DocumentInfo>> GetDocumentsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DocumentKey>> GetViewLinksAsync(CancellationToken cancellationToken);

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
