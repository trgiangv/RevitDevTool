using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Ports;

public interface IHostFilterCatalog
{
    Task<IReadOnlyList<HostFilterInfo>> GetFiltersAsync(DocumentKey document, CancellationToken cancellationToken);

    Task<FilterDefinition?> ReadAsync(DocumentKey document, long filterId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<long>> GetSelectionIdsAsync(
        DocumentKey document,
        long filterId,
        CancellationToken cancellationToken);
}
