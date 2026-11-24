using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Shared;

public sealed class FakeVisibilityExplainer : IVisibilityExplainer
{
    public List<ElementRef> Calls { get; } = [];

    public IReadOnlyList<HiddenReason> Reasons { get; set; } = [];

    public Func<ElementRef, IReadOnlyList<HiddenReason>>? ReasonsOf { get; set; }

    public TimeSpan Delay { get; set; }

    public TaskCompletionSource<bool>? Gate { get; set; }

    public Exception? Error { get; set; }

    public int Canceled { get; private set; }

    public async Task<WhyHidden> ExplainAsync(ElementRef element, CancellationToken cancellationToken)
    {
        Calls.Add(element);
        try
        {
            if (Gate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken);
            }

            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Canceled++;
            throw;
        }

        if (Error is { } error)
        {
            throw error;
        }

        return WhyHidden.Of(ReasonsOf?.Invoke(element) ?? Reasons, 1);
    }
}
