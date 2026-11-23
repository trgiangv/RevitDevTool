using CommunityToolkit.Mvvm.ComponentModel;
using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Visibility;
using System.ComponentModel;

namespace DevTools.ModelsContext.Panel;

public enum PanelTab
{
    Explorer,
    Filter
}

public sealed partial class ModelContextViewModel : ObservableObject, IDisposable
{
    public const string PanelTitle = "Model Context";

    public static readonly TimeSpan EventDelay = TimeSpan.FromMilliseconds(200);

    private readonly HostEventRelay _events;
    private readonly ContextLoader _loader;
    private readonly PanelRuntime _runtime;
    private readonly SelectionSync _selection;
    private readonly SessionSwitcher _sessions;
    private readonly VisibilityController _visibility;
    private bool _suppressContextChanged;

    public ModelContextViewModel(ModelContextServices services)
    {
        _runtime = new PanelRuntime(services);
        _visibility = new VisibilityController(_runtime);
        ContextBar = new ContextBarViewModel();
        TemporaryBar = new TemporaryBarViewModel(_runtime, _visibility);
        WhyHidden = new WhyHiddenStripViewModel(_runtime, _visibility);
        ContextMenu = new ContextMenuViewModel(_runtime, _visibility, WhyHidden);
        Explorer = new ExplorerTabViewModel(_runtime, _visibility, ContextBar, ContextMenu);
        Filter = new FilterTabViewModel(_runtime, _visibility, ContextMenu);
        _loader = new ContextLoader(_runtime, this);
        _sessions = new SessionSwitcher(_runtime, _visibility, this);
        Explorer.PropertyChanged += OnExplorerChanged;
        Explorer.RowSelectionChanged += OnExplorerRowsChanged;
        _runtime.Failed += OnFailed;
        _runtime.ActivityChanged += OnActivityChanged;
        ContextBar.Changed += OnContextChanged;
        ContextBar.Refused += OnFailed;
        ContextMenu.PropertyChanged += OnMenuChanged;
        ContextMenu.Opened += OnMenuOpened;
        TemporaryBar.PropertyChanged += OnTemporaryBarChanged;
        _selection = new SelectionSync(_runtime, _sessions, this);
        _events = new HostEventRelay(_runtime, _visibility, _sessions, this);
        services.Events.BusyChanged += OnBusyChanged;
    }

    public ContextBarViewModel ContextBar { get; }

    public ExplorerTabViewModel Explorer { get; }

    public FilterTabViewModel Filter { get; }

    public TemporaryBarViewModel TemporaryBar { get; }

    public ContextMenuViewModel ContextMenu { get; }

    public WhyHiddenStripViewModel WhyHidden { get; }

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

    public DocumentKey? ActiveModel => _sessions.Active?.Model;

    public int SessionCount => _sessions.Count;

    public JournalState Journal => _visibility.Journal;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        IsBusy = _runtime.Services.Events.IsBusy;
        var documents = await _runtime.Services.Source.GetDocumentsAsync(cancellationToken);
        if (SessionSwitcher.CurrentOf(documents) is { } model)
        {
            await _sessions.SwitchAsync(model, documents);
        }
    }

    public Task RefreshAsync()
    {
        if (_sessions.Active is not { } session || !IsPaneVisible)
        {
            return Task.CompletedTask;
        }

        var ticket = session.Begin();
        return _runtime.RunAsync(() => LoadContextAsync(ticket));
    }

    public void Dispose()
    {
        _runtime.ActivityChanged -= OnActivityChanged;
        Explorer.PropertyChanged -= OnExplorerChanged;
        Explorer.RowSelectionChanged -= OnExplorerRowsChanged;
        ContextMenu.PropertyChanged -= OnMenuChanged;
        ContextMenu.Opened -= OnMenuOpened;
        WhyHidden.Close();
        _selection.Dispose();
        _events.Dispose();
        _runtime.Services.Events.BusyChanged -= OnBusyChanged;
        _sessions.CloseAll();
        Explorer.Dispose();
        Filter.Dispose();
    }

    internal bool IsCurrent(JobTicket ticket)
    {
        return ReferenceEquals(ticket.Session, _sessions.Active)
               && !ticket.Session.IsClosed
               && ticket.Generation == ticket.Session.Generation
               && IsPaneVisible;
    }

    internal Task ResyncExplorerAsync()
    {
        return _selection.ResyncAsync();
    }

    internal void SetDocumentsQuietly(IReadOnlyList<DocumentInfo> documents, IReadOnlyCollection<DocumentKey>? ticks)
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

    internal void ShowView(ViewInfo? view)
    {
        _visibility.View = view;
    }

    internal void ShowNoModel()
    {
        Explorer.Reset(ExplorerState.Fresh(Explorer.IsSelectionSyncOn));
        Filter.Reset(new FilterState(null, Filter.Runner.IsLiveOn));
        SetDocumentsQuietly([], null);
        ShowView(null);
        IsUpdating = false;
    }

    internal void NotifyActiveModel()
    {
        OnPropertyChanged(nameof(ActiveModel));
    }

    internal void NotifySessionCount()
    {
        OnPropertyChanged(nameof(SessionCount));
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

    private async Task LoadContextAsync(JobTicket ticket)
    {
        var timer = TraceTimer.Start(_runtime.Logger);
        IsUpdating = true;
        try
        {
            var scope = ContextBar.Scope;
            var count = await _loader.LoadAsync(ticket, scope);
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

    private void Pause()
    {
        _selection.Cancel();
        _events.Pause();
    }

    private async Task ResumeAsync()
    {
        var documents = await _runtime.Services.Source.GetDocumentsAsync(CancellationToken.None);
        if (!IsPaneVisible)
        {
            return;
        }

        _events.ClearView();
        await _sessions.ResumeAsync(documents);
    }

    private void OnActivityChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Activity));
    }

    private void OnFailed(object? sender, string message)
    {
        Message = message;
    }

    private void OnMenuChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ContextMenuViewModel.Message))
        {
            Message = ContextMenu.Message;
        }
    }

    private void OnMenuOpened(object? sender, EventArgs e)
    {
        Message = null;
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

    private void OnExplorerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExplorerTabViewModel.SelectedDimmedElement))
        {
            FollowDimmedRow();
        }
    }

    private void OnExplorerRowsChanged(object? sender, EventArgs e)
    {
        FollowDimmedRow();
    }

    private void FollowDimmedRow()
    {
        if (IsExplorerTab)
        {
            _ = WhyHidden.FollowAsync(Explorer.SelectedDimmedElement);
        }
    }

    private void OnBusyChanged(object? sender, bool busy)
    {
        IsBusy = busy;
    }
}
