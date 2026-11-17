using CommunityToolkit.Mvvm.ComponentModel;
using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using System.ComponentModel;

namespace DevTools.ModelsContext.ViewModels;

public enum PanelTab
{
    Explorer,
    Filter
}

public sealed partial class ModelContextViewModel : ObservableObject, IDisposable
{
    public const string PanelTitle = "Model Context";

    public static readonly TimeSpan EventDelay = TimeSpan.FromMilliseconds(200);

    private readonly List<ElementDelta> _deltas = [];
    private readonly Debouncer _deltaDebouncer;
    private readonly Debouncer _documentsDebouncer;
    private readonly object _gate = new();
    private readonly HashSet<SourceId> _reloadedSources = [];
    private readonly PanelRuntime _runtime;
    private readonly Debouncer _selectionDebouncer;
    private readonly Dictionary<string, PanelSession> _sessions = new(StringComparer.Ordinal);
    private readonly Debouncer _viewDebouncer;
    private readonly VisibilityController _visibility;
    private PanelSession? _active;
    private IReadOnlyCollection<ElementRef>? _incomingSelection;
    private ViewInfo? _incomingView;
    private bool _suppressContextChanged;

    public ModelContextViewModel(ModelContextServices services)
    {
        _runtime = new PanelRuntime(services);
        _visibility = new VisibilityController(_runtime);
        ContextBar = new ContextBarViewModel();
        TemporaryBar = new TemporaryBarViewModel(_runtime, _visibility);
        ContextMenu = new ContextMenuViewModel(_runtime, _visibility, TemporaryBar);
        Explorer = new ExplorerTabViewModel(_runtime, _visibility, ContextBar, ContextMenu);
        Filter = new FilterTabViewModel(_runtime, _visibility, ContextMenu);
        var scheduler = _runtime.Scheduler;
        _selectionDebouncer = new Debouncer(scheduler, EventDelay, () => _ = _runtime.RunAsync(OnSelectionSettledAsync));
        _deltaDebouncer = new Debouncer(scheduler, EventDelay, () => _ = _runtime.RunAsync(OnElementsSettledAsync));
        _documentsDebouncer = new Debouncer(scheduler, EventDelay, () => _ = _runtime.RunAsync(ReloadDocumentsAsync));
        _viewDebouncer = new Debouncer(scheduler, EventDelay, () => _ = _runtime.RunAsync(OnViewSettledAsync));
        _runtime.Failed += OnFailed;
        _runtime.ActivityChanged += OnActivityChanged;
        ContextBar.Changed += OnContextChanged;
        ContextBar.Refused += OnFailed;
        TemporaryBar.PropertyChanged += OnTemporaryBarChanged;
        var events = services.Events;
        events.SelectionChanged += OnSelectionChanged;
        events.ElementsChanged += OnElementsChanged;
        events.DocumentsChanged += OnDocumentsChanged;
        events.ViewActivated += OnViewActivated;
        events.DocumentClosing += OnDocumentClosing;
        events.ActiveModelChanged += OnActiveModelChanged;
        events.ModelSynced += OnModelSynced;
        events.LinkStatusChanged += OnLinkStatusChanged;
        events.BusyChanged += OnBusyChanged;
        events.ParameterDefinitionsChanged += OnParameterDefinitionsChanged;
    }

    public ContextBarViewModel ContextBar { get; }

    public ExplorerTabViewModel Explorer { get; }

    public FilterTabViewModel Filter { get; }

    public TemporaryBarViewModel TemporaryBar { get; }

    public ContextMenuViewModel ContextMenu { get; }

    public bool IsTemporaryBarVisible => TemporaryBar.IsVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExplorerTab))]
    [NotifyPropertyChangedFor(nameof(IsFilterTab))]
    public partial PanelTab SelectedTab { get; set; }

    public bool IsExplorerTab
    {
        get => SelectedTab == PanelTab.Explorer;
        set => SelectTabWhen(value, PanelTab.Explorer);
    }

    public bool IsFilterTab
    {
        get => SelectedTab == PanelTab.Filter;
        set => SelectTabWhen(value, PanelTab.Filter);
    }

    [ObservableProperty]
    public partial bool IsUpdating { get; private set; }

    [ObservableProperty]
    public partial string? Message { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BusyText))]
    public partial bool IsBusy { get; private set; }

    public string? BusyText => IsBusy ? _runtime.BusyText : null;

    [ObservableProperty]
    public partial bool IsPaneVisible { get; set; } = true;

    public string Activity => _runtime.Activity.ToString();

    public DocumentKey? ActiveModel => _active?.Model;

    public int SessionCount => _sessions.Count;

    public JournalState Journal => _visibility.Journal;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        IsBusy = _runtime.Services.Events.IsBusy;
        var documents = await _runtime.Services.Source.GetDocumentsAsync(cancellationToken);
        if (CurrentOf(documents) is { } model)
        {
            await SwitchAsync(model, documents);
        }
    }

    public Task RefreshAsync()
    {
        if (_active is not { } session || !IsPaneVisible)
        {
            return Task.CompletedTask;
        }

        var ticket = session.Begin();
        return _runtime.RunAsync(() => LoadContextAsync(ticket));
    }

    public void Dispose()
    {
        _runtime.ActivityChanged -= OnActivityChanged;
        var events = _runtime.Services.Events;
        events.SelectionChanged -= OnSelectionChanged;
        events.ElementsChanged -= OnElementsChanged;
        events.DocumentsChanged -= OnDocumentsChanged;
        events.ViewActivated -= OnViewActivated;
        events.DocumentClosing -= OnDocumentClosing;
        events.ActiveModelChanged -= OnActiveModelChanged;
        events.ModelSynced -= OnModelSynced;
        events.LinkStatusChanged -= OnLinkStatusChanged;
        events.BusyChanged -= OnBusyChanged;
        events.ParameterDefinitionsChanged -= OnParameterDefinitionsChanged;
        _selectionDebouncer.Dispose();
        _deltaDebouncer.Dispose();
        _documentsDebouncer.Dispose();
        _viewDebouncer.Dispose();
        foreach (var session in _sessions.Values)
        {
            session.Close();
        }

        _sessions.Clear();
        _active = null;
        Explorer.Dispose();
        Filter.Dispose();
    }

    partial void OnSelectedTabChanged(PanelTab value)
    {
        _ = _runtime.RunAsync(value == PanelTab.Explorer ? ResyncExplorerAsync : Filter.ActivateAsync);
    }

    private void SelectTabWhen(bool value, PanelTab tab)
    {
        if (value)
        {
            SelectedTab = tab;
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        _runtime.IsBusy = value;
        ContextMenu.Refresh();
    }

    partial void OnIsPaneVisibleChanged(bool value)
    {
        if (value)
        {
            _ = _runtime.RunAsync(ResumeAsync);
        }
        else
        {
            Pause();
        }
    }

    private static DocumentKey? CurrentOf(IReadOnlyList<DocumentInfo> documents)
    {
        return documents.Count == 0
            ? null
            : (documents.FirstOrDefault(document => document.Key.IsCurrentModel) ?? documents[0]).Key.CurrentModel;
    }

    private bool IsCurrent(JobTicket ticket)
    {
        return ReferenceEquals(ticket.Session, _active)
               && !ticket.Session.IsClosed
               && ticket.Generation == ticket.Session.Generation
               && IsPaneVisible;
    }

    private async Task SwitchAsync(DocumentKey model, IReadOnlyList<DocumentInfo>? documents)
    {
        if (_active is { } previous)
        {
            if (previous.Model == model && documents is null)
            {
                return;
            }

            Park(previous);
        }

        if (!_sessions.TryGetValue(model.ModelId, out var session))
        {
            session = new PanelSession(model);
            _sessions.Add(model.ModelId, session);
        }

        _active = session;
        OnPropertyChanged(nameof(ActiveModel));
        OnPropertyChanged(nameof(SessionCount));
        var ticket = session.Begin();
        Explorer.Reset(session.Explorer ?? ExplorerState.Fresh(true));
        Filter.Reset(session.Filter ?? new FilterState(null, true));
        documents ??= await _runtime.Services.Source.GetDocumentsAsync(ticket.Token);
        if (!IsCurrent(ticket))
        {
            return;
        }

        await ShowSessionAsync(ticket, documents, session.Ticked);
    }

    private void Park(PanelSession session)
    {
        if (!ReferenceEquals(session, _active) || session.IsClosed)
        {
            return;
        }

        session.Explorer = Explorer.Capture();
        session.Filter = Filter.Capture();
        session.Ticked = ContextBar.TickedDocuments;
        session.Cancel();
    }

    private async Task ShowSessionAsync(
        JobTicket ticket,
        IReadOnlyList<DocumentInfo> documents,
        IReadOnlyCollection<DocumentKey>? ticks)
    {
        await ApplyReloadedLinksAsync(ticket.Token);
        if (!IsCurrent(ticket))
        {
            return;
        }

        _runtime.Forget(_runtime.Store.SetDocuments(documents));
        SetDocumentsQuietly(documents, ticks);
        var state = await _runtime.Services.ViewActions.ReadTemporaryStateAsync(ticket.Token);
        if (!IsCurrent(ticket))
        {
            return;
        }

        SetView(state?.View);
        await TemporaryBar.RefreshAsync(ticket.Token);
        await RefreshAsync();
        await ResyncExplorerAsync();
    }

    private void SetDocumentsQuietly(IReadOnlyList<DocumentInfo> documents, IReadOnlyCollection<DocumentKey>? ticks)
    {
        _suppressContextChanged = true;
        try
        {
            ContextBar.SetDocuments(documents, ticks);
        }
        finally
        {
            _suppressContextChanged = false;
        }
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
            if (await _runtime.Store.ReloadAsync(source, cancellationToken) != ReloadOutcome.Kept)
            {
                _runtime.Forget([source]);
            }
        }
    }

    private async Task LoadContextAsync(JobTicket ticket)
    {
        var timer = TraceTimer.Start(_runtime.Logger);
        IsUpdating = true;
        try
        {
            var scope = ContextBar.Scope;
            var count = await LoadScopeAsync(ticket, scope);
            if (count >= 0 && timer.IsEnabled)
            {
                _runtime.Logger.ContextLoaded(scope, count, timer.ElapsedMs);
            }
        }
        finally
        {
            if (IsCurrent(ticket))
            {
                IsUpdating = false;
            }
        }
    }

    private async Task<int> LoadScopeAsync(JobTicket ticket, ScopeKind scope)
    {
        var keys = await KeysInScopeAsync(scope, ticket.Token);
        if (!IsCurrent(ticket) || !await ShowQuickCountsAsync(ticket, scope, keys))
        {
            return -1;
        }

        if (await ReadDocumentsAsync(ticket, scope, keys) is not { } documents)
        {
            return -1;
        }

        var data = new ContextData(scope, ContextBar.CurrentModel, documents,
            ContextBar.Documents.Select(item => item.Info).ToList());
        await Explorer.LoadAsync(data, ticket.Token);
        if (!IsCurrent(ticket))
        {
            return -1;
        }

        await Filter.LoadAsync(data, ticket.Token);
        return documents.Count;
    }

    private async Task<IReadOnlyList<DocumentKey>> KeysInScopeAsync(ScopeKind scope, CancellationToken cancellationToken)
    {
        if (scope != ScopeKind.Selection)
        {
            return ContextBar.TickedDocuments;
        }

        var selection = await _runtime.Services.Selection.GetSelectionAsync(cancellationToken);
        return selection
            .Select(element => element.Document)
            .Distinct()
            .OrderBy(key => !key.IsCurrentModel)
            .ToList();
    }

    private async Task<bool> ShowQuickCountsAsync(JobTicket ticket, ScopeKind scope, IReadOnlyList<DocumentKey> keys)
    {
        var store = _runtime.Store;
        var loaded = keys.Where(key => store.Find(key) is { IsLoaded: true }).ToList();
        if (scope == ScopeKind.Selection || loaded.TrueForAll(store.IsReady))
        {
            return true;
        }

        var quick = new List<QuickDocument>();
        foreach (var key in loaded)
        {
            var groups = await _runtime.Services.Source.CountGroupsAsync(key, scope, ticket.Token);
            quick.Add(new QuickDocument(key, ContextBar.LabelOf(key), groups));
        }

        if (!IsCurrent(ticket))
        {
            return false;
        }

        Explorer.ShowQuickCounts(quick, ContextBar.CurrentModel);
        return true;
    }

    private async Task<List<DocumentScope>?> ReadDocumentsAsync(JobTicket ticket, ScopeKind scope,
        IReadOnlyList<DocumentKey> keys)
    {
        var documents = new List<DocumentScope>();
        foreach (var key in keys)
        {
            if (_runtime.Store.Find(key) is not { } info)
            {
                continue;
            }

            if (!info.IsLoaded)
            {
                AddKept(documents, info, key);
                continue;
            }

            if (await ReadLoadedAsync(ticket, scope, info, key) is not { } document)
            {
                return null;
            }

            documents.Add(document);
        }

        return documents;
    }

    private void AddKept(List<DocumentScope> documents, DocumentInfo info, DocumentKey key)
    {
        if (_runtime.Store.TryGet(key) is { } kept)
        {
            documents.Add(new DocumentScope(info, ContextBar.LabelOf(key), kept, null));
        }
    }

    private async Task<DocumentScope?> ReadLoadedAsync(JobTicket ticket, ScopeKind scope, DocumentInfo info,
        DocumentKey key)
    {
        var snapshot = await _runtime.Store.GetAsync(key, null, ticket.Token);
        if (!IsCurrent(ticket))
        {
            return null;
        }

        var ids = scope == ScopeKind.WholeModel
            ? null
            : await _runtime.Services.Source.CollectScopeAsync(key, scope, ticket.Token);
        return IsCurrent(ticket) ? new DocumentScope(info, ContextBar.LabelOf(key), snapshot, ids) : null;
    }

    private async Task ResyncExplorerAsync()
    {
        if (_active is not { } session || !IsPaneVisible)
        {
            return;
        }

        var ticket = session.Peek();
        var selection = await _runtime.Services.Selection.GetSelectionAsync(ticket.Token);
        if (IsCurrent(ticket))
        {
            await Explorer.ApplyHostSelectionAsync(selection);
        }
    }

    private void Pause()
    {
        _selectionDebouncer.Cancel();
        _deltaDebouncer.Cancel();
        _documentsDebouncer.Cancel();
        _viewDebouncer.Cancel();
        _active?.Cancel();
        List<ElementDelta> deltas;
        lock (_gate)
        {
            deltas = [.. _deltas];
            _deltas.Clear();
            _incomingSelection = null;
        }

        foreach (var delta in deltas)
        {
            MarkChanged(delta);
        }
    }

    private async Task ResumeAsync()
    {
        var documents = await _runtime.Services.Source.GetDocumentsAsync(CancellationToken.None);
        if (!IsPaneVisible)
        {
            return;
        }

        lock (_gate)
        {
            _incomingView = null;
        }

        if (CurrentOf(documents) is not { } model)
        {
            Close();
            return;
        }

        if (_active is not { } session || session.Model != model)
        {
            await SwitchAsync(model, documents);
            return;
        }

        await ShowSessionAsync(session.Begin(), documents, null);
    }

    private void MarkChanged(ElementDelta delta)
    {
        _runtime.Store.MarkDirty(delta.Source);
        _runtime.Values.Invalidate(delta.Source);
        _visibility.DropReadBack(delta.Source);
        if (delta.IsUndoRedo && _active is { } session)
        {
            _runtime.Journal.MarkStale(session.Model);
        }
    }

    private void Close()
    {
        _active = null;
        OnPropertyChanged(nameof(ActiveModel));
        Explorer.Reset(ExplorerState.Fresh(Explorer.IsSelectionSyncOn));
        Filter.Reset(new FilterState(null, Filter.Runner.IsLiveOn));
        SetDocumentsQuietly([], null);
        SetView(null);
        IsUpdating = false;
    }

    private void SetView(ViewInfo? view)
    {
        _visibility.View = view;
    }

    private void OnActivityChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Activity));
    }

    private void OnFailed(object? sender, string message)
    {
        Message = message;
    }

    private void OnContextChanged(object? sender, EventArgs e)
    {
        if (Message == ContextBarViewModel.LastDocumentHint)
        {
            Message = null;
        }

        if (!_suppressContextChanged)
        {
            _ = RefreshAsync();
        }
    }

    private void OnTemporaryBarChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TemporaryBarViewModel.IsVisible) or nameof(TemporaryBarViewModel.ChangeCount))
        {
            OnPropertyChanged(nameof(IsTemporaryBarVisible));
        }
    }

    private void OnSelectionChanged(object? sender, IReadOnlyCollection<ElementRef> selection)
    {
        if (!IsPaneVisible)
        {
            return;
        }

        lock (_gate)
        {
            _incomingSelection = selection;
        }

        _selectionDebouncer.Trigger();
    }

    private void OnElementsChanged(object? sender, ElementDelta delta)
    {
        if (!IsPaneVisible)
        {
            MarkChanged(delta);
            return;
        }

        lock (_gate)
        {
            _deltas.Add(delta);
        }

        _deltaDebouncer.Trigger();
    }

    private void OnDocumentsChanged(object? sender, EventArgs e)
    {
        if (IsPaneVisible)
        {
            _documentsDebouncer.Trigger();
        }
    }

    private void OnViewActivated(object? sender, ViewInfo view)
    {
        lock (_gate)
        {
            _incomingView = view;
        }

        if (IsPaneVisible)
        {
            _viewDebouncer.Trigger();
        }
    }

    private void OnDocumentClosing(object? sender, DocumentKey document)
    {
        if (document.IsCurrentModel)
        {
            if (_sessions.TryGetValue(document.ModelId, out var session))
            {
                session.Close();
                _sessions.Remove(document.ModelId);
                OnPropertyChanged(nameof(SessionCount));
            }

            _visibility.Forget(document);
            _runtime.Forget(_runtime.Store.DropModel(document.ModelId));
            if (_active?.Model.ModelId == document.ModelId)
            {
                Close();
            }
        }
        else
        {
            _visibility.Forget(document);
        }

        if (IsPaneVisible)
        {
            _documentsDebouncer.Trigger();
        }
    }

    private void OnActiveModelChanged(object? sender, DocumentKey model)
    {
        if (IsPaneVisible)
        {
            _ = _runtime.RunAsync(() => SwitchAsync(model.CurrentModel, null));
        }
    }

    private void OnModelSynced(object? sender, DocumentKey model)
    {
        _ = _runtime.RunAsync(() => VerifyAsync(model.CurrentModel));
    }

    private void OnLinkStatusChanged(object? sender, LinkStatusChange change)
    {
        if (change.Status == LinkStatus.Reloaded && _runtime.Store.Find(change.Link) is { } info)
        {
            lock (_gate)
            {
                _reloadedSources.Add(info.Source);
            }
        }

        if (IsPaneVisible)
        {
            _documentsDebouncer.Trigger();
        }
    }

    private void OnBusyChanged(object? sender, bool busy)
    {
        IsBusy = busy;
    }

    private void OnParameterDefinitionsChanged(object? sender, SourceId source)
    {
        _runtime.ForgetParameters(source);
        if (IsPaneVisible)
        {
            _ = _runtime.RunAsync(() => Filter.RefreshParametersAsync(source));
        }
    }

    private async Task VerifyAsync(DocumentKey model)
    {
        if (_runtime.Store.Find(model) is not { } info)
        {
            return;
        }

        var consistent = await _runtime.Store.VerifyAsync(info.Source, CancellationToken.None);
        if (!consistent && _active?.Model == model)
        {
            _runtime.Forget([info.Source]);
            await RefreshAsync();
        }
    }

    private async Task OnSelectionSettledAsync()
    {
        IReadOnlyCollection<ElementRef>? incoming;
        lock (_gate)
        {
            incoming = _incomingSelection;
            _incomingSelection = null;
        }

        if (incoming is null)
        {
            return;
        }

        var awaitingEcho = _runtime.Guard.IsPending;
        if (_runtime.Guard.ShouldDrop(incoming))
        {
            return;
        }

        if (awaitingEcho)
        {
            _runtime.Logger.SelectionMismatch(incoming.Count);
        }

        if (ContextBar.Scope == ScopeKind.Selection)
        {
            await RefreshAsync();
        }

        if (SelectedTab == PanelTab.Explorer)
        {
            var timer = TraceTimer.Start(_runtime.Logger);
            await Explorer.ApplyHostSelectionAsync(incoming);
            if (timer.IsEnabled)
            {
                _runtime.Logger.SelectionReceived(incoming.Count, timer.ElapsedMs);
            }
        }
    }

    private async Task OnElementsSettledAsync()
    {
        List<ElementDelta> deltas;
        lock (_gate)
        {
            deltas = [.. _deltas];
            _deltas.Clear();
        }

        var timer = TraceTimer.Start(_runtime.Logger);
        foreach (var delta in deltas)
        {
            _runtime.Store.Apply(delta);
            _runtime.Values.Invalidate(delta.Source);
            _visibility.DropReadBack(delta.Source);
            if (delta.IsUndoRedo && _active is { } session)
            {
                _runtime.Journal.MarkStale(session.Model);
            }
        }

        if (timer.IsEnabled)
        {
            _runtime.Logger.DeltasApplied(deltas.Count, deltas.Sum(delta => delta.ChangedCount), timer.ElapsedMs);
        }

        await RefreshAsync();
    }

    private async Task ReloadDocumentsAsync()
    {
        var session = _active;
        var documents = await _runtime.Services.Source.GetDocumentsAsync(CancellationToken.None);
        if (!ReferenceEquals(session, _active) || !IsPaneVisible)
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
        if (!ReferenceEquals(session, _active))
        {
            return;
        }

        _runtime.Forget(_runtime.Store.SetDocuments(documents));
        ContextBar.SetDocuments(documents);
    }

    private async Task OnViewSettledAsync()
    {
        ViewInfo? view;
        lock (_gate)
        {
            view = _incomingView;
            _incomingView = null;
        }

        if (view is null)
        {
            return;
        }

        if (_active is not { } session || session.Model != view.Model.CurrentModel)
        {
            await SwitchAsync(view.Model.CurrentModel, null);
            return;
        }

        SetView(view);
        await TemporaryBar.RefreshAsync(session.Token);
        await RefreshAsync();
    }
}
