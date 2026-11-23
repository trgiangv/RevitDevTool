using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Visibility;

public interface IViewActions
{
    Task<IReadOnlyList<ElementRef>> ExecuteAsync(VisibilityPlan plan, CancellationToken cancellationToken);

    string? TakeNotice();

    Task ShowLinkedAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken);

    Task KeepAsync(CancellationToken cancellationToken);

    Task ResetAsync(CancellationToken cancellationToken);

    Task ZoomAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken);

    Task SectionBoxAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken);

    Task<TemporaryState?> ReadTemporaryStateAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<long>> GetIdsHiddenPermanentlyOrTemporarilyAsync(
        DocumentKey document,
        IReadOnlyCollection<long> candidates,
        CancellationToken cancellationToken);
}
