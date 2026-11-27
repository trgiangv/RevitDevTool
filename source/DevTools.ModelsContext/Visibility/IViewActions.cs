using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Visibility;

public interface IViewActions
{
    Task<PlanOutcome> ExecuteAsync(VisibilityPlan plan, CancellationToken cancellationToken);

    Task ShowLinkedAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken);

    Task KeepAsync(string? viewId, CancellationToken cancellationToken);

    Task<ReadBackResult?> ResetAsync(string? viewId, ReadBackRequest? request, CancellationToken cancellationToken);

    Task ZoomAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken);

    Task SectionBoxAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken);

    Task<ActiveView?> ReadActiveViewAsync(CancellationToken cancellationToken);

    Task<ReadBackResult> ReadBackAsync(ReadBackRequest request, CancellationToken cancellationToken);
}

public sealed record PlanOutcome(IReadOnlyList<ElementRef> LinkHides, string? Notice = null, ReadBackResult? ReadBack = null);

public sealed record ReadBackRequest(DocumentKey Document, IReadOnlyList<long> Ids);

/// <param name="Document">The Current model the call read.</param>
/// <param name="Read">The ids the call covered: every requested id on the queued path, the first slice inline.</param>
/// <param name="Hidden">The covered ids Revit hides in the active view.</param>
public sealed record ReadBackResult(DocumentKey Document, IReadOnlyCollection<long> Read, IReadOnlyCollection<long> Hidden);

public sealed record ActiveView(ViewInfo View, bool IsTemporary);
