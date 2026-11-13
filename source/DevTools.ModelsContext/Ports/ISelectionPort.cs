using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Ports;

public interface ISelectionPort
{
    Task<IReadOnlyCollection<ElementRef>> GetSelectionAsync(CancellationToken cancellationToken);

    Task SelectAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken);
}
