using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Visibility;

public interface IViewActions
{
    Task<PlanOutcome> ExecuteAsync(VisibilityPlan plan, CancellationToken cancellationToken);

    Task ShowLinkedAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken);

    Task KeepAsync(string? viewId, CancellationToken cancellationToken);

    Task<HiddenRead?> ResetAsync(string? viewId, HiddenProbe? readBack, CancellationToken cancellationToken);

    Task ZoomAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken);

    Task SectionBoxAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken);

    Task<ActiveView?> ReadActiveViewAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<long>> ReadHiddenAsync(
        DocumentKey document,
        IReadOnlyCollection<long> candidates,
        CancellationToken cancellationToken);
}

public sealed record PlanOutcome(IReadOnlyList<ElementRef> LinkHides, string? Notice = null, HiddenRead? ReadBack = null)
{
    public static PlanOutcome None { get; } = new([]);
}

public sealed record HiddenProbe(DocumentKey Document, IReadOnlyList<long> Candidates);

public sealed record HiddenRead(DocumentKey Document, IReadOnlyCollection<long> Checked, IReadOnlyCollection<long> Hidden);

public sealed record ActiveView(ViewInfo View, bool IsTemporary);
