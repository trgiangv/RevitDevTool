using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Ports;

public interface IModelSource
{
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
}
