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

    private readonly List<DocumentKey> _closing = [];
    private readonly List<ElementDelta> _deltas = [];
    private readonly Debouncer _deltaDebouncer;
    private readonly Debouncer _documentsDebouncer;
    private readonly object _gate = new();
    private readonly PanelRuntime _runtime;
    private readonly Debouncer _selectionDebouncer;
    private readonly Debouncer _viewDebouncer;
    private readonly VisibilityController _visibility;
    private CancellationTokenSource _context = new();
    private IReadOnlyCollection<ElementRef>? _incomingSelection;
    private ViewInfo? _incomingView;
    private bool _initialized;
    private bool _isUpdating;
    private string? _message;
    private PanelTab _selectedTab;
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

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var documents = await _runtime.Services.Source.GetDocumentsAsync(cancellationToken);
        _runtime.Store.SetDocuments(documents);
        ContextBar.SetDocuments(documents);
        var state = await _runtime.Services.ViewActions.ReadTemporaryStateAsync(cancellationToken);
        SetView(state?.View);
        await TemporaryBar.RefreshAsync(cancellationToken);
        _initialized = true;
        await RefreshAsync();
        await ResyncTreeAsync();
    }

    public Task RefreshAsync()
    {
        _context.Cancel();
        _context.Dispose();
        _context = new CancellationTokenSource();
        var token = _context.Token;
        return _runtime.RunAsync(() => LoadContextAsync(token));
    }

    public void Dispose()
    {
        var events = _runtime.Services.Events;
        events.SelectionChanged -= OnSelectionChanged;
        events.ElementsChanged -= OnElementsChanged;
        events.DocumentsChanged -= OnDocumentsChanged;
        events.ViewActivated -= OnViewActivated;
        events.DocumentClosing -= OnDocumentClosing;
        _selectionDebouncer.Dispose();
        _deltaDebouncer.Dispose();
        _documentsDebouncer.Dispose();
        _viewDebouncer.Dispose();
        _context.Cancel();
        _context.Dispose();
        Tree.Dispose();
        Filter.Dispose();
    }

    private async Task LoadContextAsync(CancellationToken cancellationToken)
    {
        IsUpdating = true;
        try
        {
            var services = _runtime.Services;
            var scope = ContextBar.Scope;
            IReadOnlyList<DocumentKey> keys = scope == ScopeKind.Selection
                ? (await services.Selection.GetSelectionAsync(cancellationToken))
                .Select(element => element.Document)
                .Distinct()
                .OrderBy(key => !key.IsCurrentModel)
                .ToList()
                : ContextBar.TickedDocuments;
            var documents = new List<DocumentScope>();
            foreach (var key in keys)
            {
                if (_runtime.Store.Find(key) is not { IsLoaded: true } info)
                {
                    continue;
                }

                var snapshot = await _runtime.Store.GetAsync(key, null, cancellationToken);
                var ids = scope == ScopeKind.WholeModel
                    ? null
                    : await services.Source.CollectScopeAsync(key, scope, cancellationToken);
                documents.Add(new DocumentScope(info, ContextBar.LabelOf(key), snapshot, ids));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var data = new ContextData(scope, ContextBar.CurrentModel, documents);
            await Tree.LoadAsync(data, cancellationToken);
            await Filter.LoadAsync(data, cancellationToken);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsUpdating = false;
            }
        }
    }

    private async Task ResyncTreeAsync()
    {
        var selection = await _runtime.Services.Selection.GetSelectionAsync(_context.Token);
        await Tree.ApplyRevitSelectionAsync(selection);
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
        if (_initialized)
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
        lock (_gate)
        {
            _incomingSelection = selection;
        }

        _selectionDebouncer.Trigger();
    }

    private void OnElementsChanged(object? sender, ElementDelta delta)
    {
        lock (_gate)
        {
            _deltas.Add(delta);
        }

        _deltaDebouncer.Trigger();
    }

    private void OnDocumentsChanged(object? sender, EventArgs e)
    {
        _documentsDebouncer.Trigger();
    }

    private void OnViewActivated(object? sender, ViewInfo view)
    {
        lock (_gate)
        {
            _incomingView = view;
        }

        _viewDebouncer.Trigger();
    }

    private void OnDocumentClosing(object? sender, DocumentKey document)
    {
        lock (_gate)
        {
            _closing.Add(document);
        }

        _documentsDebouncer.Trigger();
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
            await Tree.ApplyRevitSelectionAsync(incoming);
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
            if (delta.IsUndoRedo && ContextBar.CurrentModel is { } current)
            {
                _runtime.Journal.MarkStale(current);
            }
        }

        await RefreshAsync();
    }

    private async Task ReloadDocumentsAsync()
    {
        List<DocumentKey> closing;
        lock (_gate)
        {
            closing = [.. _closing];
            _closing.Clear();
        }

        foreach (var document in closing)
        {
            _visibility.Forget(document);
        }

        var documents = await _runtime.Services.Source.GetDocumentsAsync(_context.Token);
        _runtime.Store.SetDocuments(documents);
        ContextBar.SetDocuments(documents);
    }

    private async Task OnViewSettledAsync()
    {
        ViewInfo? view;
        lock (_gate)
        {
            view = _incomingView;
        }

        SetView(view);
        await TemporaryBar.RefreshAsync(_context.Token);
        await RefreshAsync();
    }
}
