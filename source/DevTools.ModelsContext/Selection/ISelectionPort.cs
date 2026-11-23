using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Selection;

public interface ISelectionPort
{
    Task<IReadOnlyCollection<ElementRef>> GetSelectionAsync(CancellationToken cancellationToken);

    Task SelectAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken);

    Task<PickResult> PickAsync(
        IReadOnlyCollection<DocumentKey> links,
        IReadOnlyCollection<long> categoryIds,
        CancellationToken cancellationToken);
}
