using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.ViewModels;

internal sealed class PanelSession(DocumentKey model)
{
    private CancellationTokenSource _jobs = new();

    public DocumentKey Model { get; } = model;

    public int Generation { get; private set; }

    public bool IsClosed { get; private set; }

    public CancellationToken Token => _jobs.Token;

    public ExplorerState? Explorer { get; set; }

    public FilterState? Filter { get; set; }

    public IReadOnlyList<DocumentKey>? Ticked { get; set; }

    public JobTicket Begin()
    {
        _jobs.Cancel();
        _jobs.Dispose();
        _jobs = new CancellationTokenSource();
        Generation++;
        return new JobTicket(this, Generation, _jobs.Token);
    }

    public JobTicket Peek()
    {
        return new JobTicket(this, Generation, _jobs.Token);
    }

    public void Cancel()
    {
        _jobs.Cancel();
        Generation++;
    }

    public void Close()
    {
        Cancel();
        IsClosed = true;
    }
}

internal readonly record struct JobTicket(PanelSession Session, int Generation, CancellationToken Token);

internal sealed record ExplorerState(HashSet<string> Expanded, HashSet<string> Roots, bool IsSelectionSyncOn)
{
    public static ExplorerState Fresh(bool isSelectionSyncOn)
    {
        return new ExplorerState(new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal),
            isSelectionSyncOn);
    }
}

internal sealed record FilterState(FilterSourceItem? Selected, bool IsLiveOn, FilterDraft? Draft = null);

internal sealed record FilterDraft(FilterDefinition Definition, string Name);

internal sealed record QuickDocument(DocumentKey Key, string Label, IReadOnlyList<GroupCount> Groups);
