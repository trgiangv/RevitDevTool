using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Visibility;

public interface IViewActions
{
    Task<PlanOutcome> ExecuteAsync(VisibilityPlan plan, CancellationToken cancellationToken);

    Task ShowLinkedAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken);

    Task KeepAsync(string? viewId, CancellationToken cancellationToken);

    Task ResetAsync(string? viewId, CancellationToken cancellationToken);

    Task ZoomAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken);

    Task SectionBoxAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken);

    Task<ActiveView?> ReadActiveViewAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<long>> ReadHiddenAsync(
        DocumentKey document,
        IReadOnlyCollection<long> candidates,
        CancellationToken cancellationToken);
}

public sealed record PlanOutcome(IReadOnlyList<ElementRef> LinkHides, string? Notice = null)
{
    public static PlanOutcome None { get; } = new([]);
}

public sealed record ActiveView(ViewInfo View, bool IsTemporary);
