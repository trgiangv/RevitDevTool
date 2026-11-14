using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Ports;

public interface IHostFilterCatalog
{
    Task<IReadOnlyList<HostFilterInfo>> GetFiltersAsync(DocumentKey document, CancellationToken cancellationToken);

    Task<FilterDefinition?> ReadAsync(DocumentKey document, long filterId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<long>> EvaluateAsync(
        DocumentKey document,
        long filterId,
        IReadOnlyCollection<long>? scope,
        CancellationToken cancellationToken);
}
