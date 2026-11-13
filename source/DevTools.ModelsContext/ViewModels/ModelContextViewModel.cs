using CommunityToolkit.Mvvm.ComponentModel;
using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using System.ComponentModel;

namespace DevTools.ModelsContext.ViewModels;

public enum PanelTab
{
    Tree,
    Filter
}

public sealed class ModelContextViewModel : ObservableObject, IDisposable
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
    private bool _isBusy;
    private bool _isPaneVisible = true;
    private bool _isUpdating;
    private string? _message;
    private PanelTab _selectedTab;
    private bool _suppressContextChanged;
    private string _viewName = string.Empty;

    public ModelContextViewModel(ModelContextServices services)
    {
        _runtime = new PanelRuntime(services);
        _visibility = new VisibilityController(_runtime);
        ContextBar = new ContextBarViewModel();
        TemporaryBar = new TemporaryBarViewModel(_runtime, _visibility);
        ContextMenu = new ContextMenuViewModel(_runtime, _visibility, TemporaryBar);
        Tree = new TreeTabViewModel(_runtime, _visibility, ContextBar, ContextMenu);
        Filter = new FilterTabViewModel(_runtime, _visibility, ContextMenu);
        var scheduler = services.Scheduler;
        _selectionDebouncer = new Debouncer(scheduler, EventDelay, () => _ = _runtime.RunAsync(OnSelectionSettledAsync));
        _deltaDebouncer = new Debouncer(scheduler, EventDelay, () => _ = _runtime.RunAsync(OnElementsSettledAsync));
        _documentsDebouncer = new Debouncer(scheduler, EventDelay, () => _ = _runtime.RunAsync(ReloadDocumentsAsync));
        _viewDebouncer = new Debouncer(scheduler, EventDelay, () => _ = _runtime.RunAsync(OnViewSettledAsync));
        _runtime.Failed += OnFailed;
        ContextBar.Changed += OnContextChanged;
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

    public TreeTabViewModel Tree { get; }

    public FilterTabViewModel Filter { get; }

    public TemporaryBarViewModel TemporaryBar { get; }

    public ContextMenuViewModel ContextMenu { get; }

    public string ViewName
    {
        get => _viewName;
        private set
        {
            if (SetProperty(ref _viewName, value))
            {
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    public string Title => ViewName.Length == 0 ? PanelTitle : $"{PanelTitle} · {ViewName}";

    public bool IsTemporaryBarVisible => TemporaryBar.IsVisible;

    public PanelTab SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (!SetProperty(ref _selectedTab, value))
            {
                return;
            }

            _ = _runtime.RunAsync(value == PanelTab.Tree ? ResyncTreeAsync : Filter.ActivateAsync);
        }
    }

    public bool IsUpdating
    {
        get => _isUpdating;
        private set
        {
            if (SetProperty(ref _isUpdating, value))
            {
                Tree.IsUpdating = value;
            }
        }
    }

    public string? Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            _runtime.IsBusy = value;
            OnPropertyChanged(nameof(BusyText));
            ContextMenu.Refresh();
        }
    }

    public string? BusyText => IsBusy ? _runtime.BusyText : null;

    public bool IsPaneVisible
    {
        get => _isPaneVisible;
        set
        {
            if (!SetProperty(ref _isPaneVisible, value))
            {
                return;
            }

            if (value)
            {
                _ = _runtime.RunAsync(ResumeAsync);
            }
            else
            {
                Pause();
            }
        }
    }

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
        if (_active is not { } session || !_isPaneVisible)
        {
            return Task.CompletedTask;
        }

        var ticket = session.Begin();
        return _runtime.RunAsync(() => LoadContextAsync(ticket));
    }

    public void Dispose()
    {
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
        Tree.Dispose();
        Filter.Dispose();
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
               && _isPaneVisible;
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
        Tree.Reset(session.Tree ?? TreeState.Fresh(true));
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

        session.Tree = Tree.Capture();
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
        await ResyncTreeAsync();
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
        var cancellationToken = ticket.Token;
        IsUpdating = true;
        try
        {
            var services = _runtime.Services;
            var store = _runtime.Store;
            var scope = ContextBar.Scope;
            IReadOnlyList<DocumentKey> keys = scope == ScopeKind.Selection
                ? (await services.Selection.GetSelectionAsync(cancellationToken))
                .Select(element => element.Document)
                .Distinct()
                .OrderBy(key => !key.IsCurrentModel)
                .ToList()
                : ContextBar.TickedDocuments;
            if (!IsCurrent(ticket))
            {
                return;
            }

            var loaded = keys.Where(key => store.Find(key) is { IsLoaded: true }).ToList();
            if (scope != ScopeKind.Selection && loaded.Any(key => !store.IsReady(key)))
            {
                var quick = new List<QuickDocument>();
                foreach (var key in loaded)
                {
                    var groups = await services.Source.CountGroupsAsync(key, scope, cancellationToken);
                    quick.Add(new QuickDocument(key, ContextBar.LabelOf(key), groups));
                }

                if (!IsCurrent(ticket))
                {
                    return;
                }

                Tree.ShowQuickCounts(quick, ContextBar.CurrentModel);
            }

            var documents = new List<DocumentScope>();
            foreach (var key in keys)
            {
                if (store.Find(key) is not { } info)
                {
                    continue;
                }

                if (!info.IsLoaded)
                {
                    if (store.TryGet(key) is { } kept)
                    {
                        documents.Add(new DocumentScope(info, ContextBar.LabelOf(key), kept, null));
                    }

                    continue;
                }

                var snapshot = await store.GetAsync(key, null, cancellationToken);
                if (!IsCurrent(ticket))
                {
                    return;
                }

                var ids = scope == ScopeKind.WholeModel
                    ? null
                    : await services.Source.CollectScopeAsync(key, scope, cancellationToken);
                if (!IsCurrent(ticket))
                {
                    return;
                }

                documents.Add(new DocumentScope(info, ContextBar.LabelOf(key), snapshot, ids));
            }

            foreach (var document in documents.Where(document => document.Key.IsCurrentModel && document.Info.IsLoaded))
            {
                await _visibility.ReadBackAsync(document.Key, cancellationToken);
            }

            if (!IsCurrent(ticket))
            {
                return;
            }

            var data = new ContextData(scope, ContextBar.CurrentModel, documents);
            await Tree.LoadAsync(data, cancellationToken);
            if (!IsCurrent(ticket))
            {
                return;
            }

            await Filter.LoadAsync(data, cancellationToken);
        }
        finally
        {
            if (IsCurrent(ticket))
            {
                IsUpdating = false;
            }
        }
    }

    private async Task ResyncTreeAsync()
    {
        if (_active is not { } session || !_isPaneVisible)
        {
            return;
        }

        var ticket = session.Peek();
        var selection = await _runtime.Services.Selection.GetSelectionAsync(ticket.Token);
        if (IsCurrent(ticket))
        {
            await Tree.ApplyHostSelectionAsync(selection);
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
        if (!_isPaneVisible)
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
        if (delta.IsUndoRedo && _active is { } session)
        {
            _runtime.Journal.MarkStale(session.Model);
        }
    }

    private void Close()
    {
        _active = null;
        OnPropertyChanged(nameof(ActiveModel));
        Tree.Reset(TreeState.Fresh(Tree.IsSyncOn));
        Filter.Reset(new FilterState(null, Filter.IsLiveOn));
        SetDocumentsQuietly([], null);
        SetView(null);
        IsUpdating = false;
    }

    private void SetView(ViewInfo? view)
    {
        _visibility.View = view;
        ViewName = view?.Name ?? string.Empty;
    }

    private void OnFailed(object? sender, string message)
    {
        Message = message;
    }

    private void OnContextChanged(object? sender, EventArgs e)
    {
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
        if (!_isPaneVisible)
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
        if (!_isPaneVisible)
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
        if (_isPaneVisible)
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

        if (_isPaneVisible)
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

        if (_isPaneVisible)
        {
            _documentsDebouncer.Trigger();
        }
    }

    private void OnActiveModelChanged(object? sender, DocumentKey model)
    {
        if (_isPaneVisible)
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

        if (_isPaneVisible)
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
        if (_isPaneVisible)
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

        if (incoming is null || _runtime.Guard.ShouldDrop(incoming))
        {
            return;
        }

        if (ContextBar.Scope == ScopeKind.Selection)
        {
            await RefreshAsync();
        }

        if (SelectedTab == PanelTab.Tree)
        {
            await Tree.ApplyHostSelectionAsync(incoming);
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

        foreach (var delta in deltas)
        {
            _runtime.Store.Apply(delta);
            _runtime.Values.Invalidate(delta.Source);
            if (delta.IsUndoRedo && _active is { } session)
            {
                _runtime.Journal.MarkStale(session.Model);
            }
        }

        await RefreshAsync();
    }

    private async Task ReloadDocumentsAsync()
    {
        var session = _active;
        var documents = await _runtime.Services.Source.GetDocumentsAsync(CancellationToken.None);
        if (!ReferenceEquals(session, _active) || !_isPaneVisible)
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
