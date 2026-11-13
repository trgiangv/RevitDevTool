using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Ports;

public interface IViewActions
{
    Task ExecuteAsync(VisibilityPlan plan, CancellationToken cancellationToken);

    Task KeepAsync(CancellationToken cancellationToken);

    Task ResetAsync(CancellationToken cancellationToken);

    Task ZoomAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken);

    Task SectionBoxAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken);

    Task<TemporaryState?> ReadTemporaryStateAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<long>> GetIdsHiddenPermanentlyOrTemporarilyAsync(DocumentKey document, CancellationToken cancellationToken);
}
