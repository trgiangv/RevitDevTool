using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Panel;

internal sealed class SessionSwitcher(PanelRuntime runtime, VisibilityController visibility, ModelContextViewModel owner)
{
    private readonly object _gate = new();
    private readonly HashSet<SourceId> _reloadedSources = [];
    private readonly Dictionary<string, PanelSession> _sessions = new(StringComparer.Ordinal);

    public PanelSession? Active { get; private set; }

    public int Count => _sessions.Count;

    public static DocumentKey? CurrentOf(IReadOnlyList<DocumentInfo> documents)
    {
        return documents.Count == 0
            ? null
            : (documents.FirstOrDefault(document => document.Key.IsCurrentModel) ?? documents[0]).Key.CurrentModel;
    }

    public async Task SwitchAsync(DocumentKey model, IReadOnlyList<DocumentInfo>? documents)
    {
        if (Active is { } previous)
        {
            if (previous.Model == model && documents is null)
            {
                return;
            }

            Park(previous);
        }

        var session = SessionOf(model);
        Active = session;
        owner.NotifyActiveModel();
        owner.NotifySessionCount();
        var ticket = session.Begin();
        owner.Explorer.Reset(session.Explorer ?? ExplorerState.Fresh(true));
        owner.Filter.Reset(session.Filter ?? new FilterState(null, true));
        documents ??= await runtime.Services.Source.GetDocumentsAsync(ticket.Token);
        if (!owner.IsCurrent(ticket))
        {
            return;
        }

        await ShowAsync(ticket, documents, session.Ticked);
    }

    public async Task ResumeAsync(IReadOnlyList<DocumentInfo> documents)
    {
        if (CurrentOf(documents) is not { } model)
        {
            Close();
            return;
        }

        if (Active is not { } session || session.Model != model)
        {
            await SwitchAsync(model, documents);
            return;
        }

        await ShowAsync(session.Begin(), documents, null);
    }

    public async Task ReloadDocumentsAsync()
    {
        var session = Active;
        var documents = await runtime.Services.Source.GetDocumentsAsync(CancellationToken.None);
        if (!ReferenceEquals(session, Active) || !owner.IsPaneVisible)
        {
            return;
        }

        if (CurrentOf(documents) is not { } model)
        {
            Close();
            return;
        }

        if (session is null || session.Model != model)
        {
            await SwitchAsync(model, documents);
            return;
        }

        await ApplyReloadedLinksAsync(CancellationToken.None);
        if (!ReferenceEquals(session, Active))
        {
            return;
        }

        runtime.Forget(runtime.Store.SetDocuments(documents));
        owner.ContextBar.SetDocuments(documents);
    }

    public async Task VerifyAsync(DocumentKey model)
    {
        if (runtime.Store.Find(model) is not { } info)
        {
            return;
        }

        var consistent = await runtime.Store.VerifyAsync(info.Source, CancellationToken.None);
        if (!consistent && Active?.Model == model)
        {
            runtime.Forget([info.Source]);
            await owner.RefreshAsync();
        }
    }

    public void AddReloaded(SourceId source)
    {
        lock (_gate)
        {
            _reloadedSources.Add(source);
        }
    }

    public void Forget(DocumentKey document)
    {
        if (!document.IsCurrentModel)
        {
            visibility.Forget(document);
            return;
        }

        if (_sessions.TryGetValue(document.ModelId, out var session))
        {
            session.Close();
            _sessions.Remove(document.ModelId);
            owner.NotifySessionCount();
        }

        visibility.Forget(document);
        runtime.Forget(runtime.Store.DropModel(document.ModelId));
        if (Active?.Model.ModelId == document.ModelId)
        {
            Close();
        }
    }

    public void CloseAll()
    {
        foreach (var session in _sessions.Values)
        {
            session.Close();
        }

        _sessions.Clear();
        Active = null;
    }

    private PanelSession SessionOf(DocumentKey model)
    {
        if (!_sessions.TryGetValue(model.ModelId, out var session))
        {
            session = new PanelSession(model);
            _sessions.Add(model.ModelId, session);
        }

        return session;
    }

    private void Park(PanelSession session)
    {
        if (!ReferenceEquals(session, Active) || session.IsClosed)
        {
            return;
        }

        session.Explorer = owner.Explorer.Capture();
        session.Filter = owner.Filter.Capture();
        session.Ticked = owner.ContextBar.TickedDocuments;
        session.Cancel();
    }

    private void Close()
    {
        Active = null;
        owner.NotifyActiveModel();
        owner.ShowNoModel();
    }

    private async Task ShowAsync(
        JobTicket ticket,
        IReadOnlyList<DocumentInfo> documents,
        IReadOnlyCollection<DocumentKey>? ticks)
    {
        await ApplyReloadedLinksAsync(ticket.Token);
        if (!owner.IsCurrent(ticket))
        {
            return;
        }

        runtime.Forget(runtime.Store.SetDocuments(documents));
        owner.SetDocumentsQuietly(documents, ticks);
        var state = await runtime.Services.ViewActions.ReadTemporaryStateAsync(ticket.Token);
        if (!owner.IsCurrent(ticket))
        {
            return;
        }

        owner.ShowView(state?.View);
        await owner.TemporaryBar.RefreshAsync(ticket.Token);
        await owner.RefreshAsync();
        await owner.ResyncExplorerAsync();
    }

    private async Task ApplyReloadedLinksAsync(CancellationToken cancellationToken)
    {
        List<SourceId> sources;
        lock (_gate)
        {
            sources = [.. _reloadedSources];
            _reloadedSources.Clear();
        }

        foreach (var source in sources)
        {
            if (await runtime.Store.ReloadAsync(source, cancellationToken) != ReloadOutcome.Kept)
            {
                runtime.Forget([source]);
            }
        }
    }
}
