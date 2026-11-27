using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Explorer;

public sealed partial class ExplorerTabViewModel : ObservableObject, IDisposable
{
    public const int InstancePageSize = 200;
    public const int OpenPathLimit = 50;
    public const int SelectionSyncPauseLimit = 5000;
    public const string ExpandAllText = "Expand all";
    public const string TooManyHint = "Too many to reveal · Reveal all";

    public static readonly TimeSpan SelectionSyncDelay = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(150);

    private readonly ContextMenuViewModel _menu;
    private readonly ExplorerRows _rows;
    private readonly PanelRuntime _runtime;
    private readonly ExplorerSearch _search;
    private readonly Debouncer _searchDebouncer;
    private readonly ExplorerSelection _selection = new();
    private readonly DocumentStates _states;
    private readonly RowSelectionSync _sync;
    private readonly RowTargets _targets;
    private readonly ExplorerTree _tree;
    private readonly VisibilityController _visibility;
    private readonly Action<string?> _report;
    private ExplorerState? _pending;

    private int _building;
    private bool _hiding;
    private bool _repaintPending;

    internal ExplorerTabViewModel(
        PanelRuntime runtime,
        VisibilityController visibility,
        ContextBarViewModel bar,
        ContextMenuViewModel menu,
        Action<string?> report)
    {
        _runtime = runtime;
        _report = report;
        _visibility = visibility;
        _menu = menu;
        _states = new DocumentStates(visibility);
        Categories = new ExplorerCategories(runtime, visibility, bar, menu, () => _tree!.Token, OnChecksChanged,
            report);
        Pick = new ExplorerPick(runtime, () => bar.CheckedDocuments, () => Categories.Checked,
            () => IsSelectionSyncing);
        _tree = new ExplorerTree(runtime, _states, Categories);
        var reveal = new TreeReveal(runtime, _tree);
        _search = new ExplorerSearch(runtime, _tree, reveal);
        _rows = new ExplorerRows(runtime, visibility, _states, _tree, _search, _selection, Categories);
        _sync = new RowSelectionSync(runtime, bar, _tree, reveal, _rows, _selection);
        _targets = new RowTargets(runtime, _selection, menu);
        _searchDebouncer = new Debouncer(runtime.Scheduler, SearchDelay,
            () => _ = _runtime.RunAsync(ApplySearchAsync));
        _sync.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
        _sync.RowSelectionChanged += (_, e) =>
        {
            UpdateDimmed();
            RowSelectionChanged?.Invoke(this, e);
        };
        _sync.ScrollRequested += (_, row) => ScrollRequested?.Invoke(this, row);
        _visibility.Changed += (_, _) => OnVisibilityChanged();
        _visibility.Watch(() => VisibleIds.Of(Data, _search.Visible()));
    }

    private ContextData Data
    {
        get => _runtime.Data;
        set => _runtime.Data = value;
    }

    public event EventHandler? RowSelectionChanged;

    public event EventHandler<TreeRow>? ScrollRequested;

    public RangeObservableCollection<TreeRow> Rows => _rows.Items;

    public ExplorerCategories Categories { get; }

    public ExplorerPick Pick { get; }

    [ObservableProperty]
    public partial ElementRef? SelectedDimmedElement { get; private set; }

    public IReadOnlyCollection<TreeRow> SelectedRows => _selection.Rows;

    public IReadOnlyCollection<ElementRef> SelectedElements => _selection.Elements;

    public string SearchText
    {
        get;
        set
        {
            if (SetProperty(ref field, value ?? string.Empty))
            {
                _searchDebouncer.Trigger();
            }
        }
    } = string.Empty;

    public bool IsSelectionSyncOn
    {
        get => _sync.IsSelectionSyncOn;
        set => _sync.IsSelectionSyncOn = value;
    }

    public bool IsSelectionSyncVisible => _sync.IsSelectionSyncVisible;

    public bool IsSelectionSyncPaused => _sync.IsSelectionSyncPaused;

    public bool IsSelectionSyncing => _sync.IsSelectionSyncing;

    public string? SelectionSyncHint => _sync.SelectionSyncHint;

    public int SelectedCount => _sync.SelectedCount;

    public bool HasSelection => _sync.HasSelection;

    internal bool IsSending => _sync.IsSending;

    public string StatusText => _sync.StatusText;

    public bool CanShowInExplorer => _sync.CanShowInExplorer;


    public void Dispose()
    {
        _sync.Dispose();
        _searchDebouncer.Dispose();
    }

    public Task ExpandAsync(TreeRow row)
    {
        return _rows.ExpandAsync(row);
    }

    public void Collapse(TreeRow row)
    {
        _rows.Collapse(row);
    }

    public Task ExpandTreeAsync()
    {
        return _rows.ExpandAllAsync();
    }

    public void CollapseTree()
    {
        _rows.CollapseAll();
    }

    public void SetSelectedRows(IEnumerable<TreeRow> rows)
    {
        _sync.SetSelectedRows(rows);
        UpdateDimmed();
    }

    public IReadOnlyList<ElementRef> ElementsOf(IEnumerable<TreeRow> rows)
    {
        return Data.ElementsOf(rows);
    }

    public void HighlightTargets(TreeRow? row)
    {
        _targets.Highlight(row, Rows);
    }

    public void ClearHighlight()
    {
        _targets.ClearHighlight();
    }

    public string? ReasonFor(TreeRow row, MenuAction action)
    {
        return _targets.ReasonFor(row, action, IsSelectionSyncing, _tree.Token);
    }

    public void OpenMenu(TreeRow? clicked)
    {
        if (clicked is not null && !_selection.Rows.Contains(clicked))
        {
            SetSelectedRows([clicked]);
            RowSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        _targets.Open(IsSelectionSyncing, _tree.Token);
    }

    public async Task RunToolAsync(TreeRow row, MenuAction action)
    {
        _targets.OpenFor(row, IsSelectionSyncing, _tree.Token);
        var item = _menu[action];
        if (!item.IsEnabled)
        {
            _report(item.DisabledReason);
            return;
        }

        _report(null);
        await item.Command.ExecuteAsync(null);
    }

    internal ExplorerState Capture()
    {
        if (_pending is { } pending)
        {
            return pending with { IsSelectionSyncOn = IsSelectionSyncOn };
        }

        return new ExplorerState(_tree.ExpandedKeys(), _tree.RootKeys(), IsSelectionSyncOn);
    }

    internal void Reset(ExplorerState state)
    {
        _pending = state;
        ClearHighlight();
        Data = ContextData.Empty;
        _tree.Forget();
        _states.Clear();
        _search.Clear();
        _sync.Reset(state.IsSelectionSyncOn);
        Pick.Clear();
        Rows.ReplaceAll([]);
        _sync.RaiseSelectionCounts();
        _sync.RaiseRowSelectionChanged();
    }

    internal void ShowQuickCounts(IReadOnlyList<QuickDocument> documents, DocumentKey? currentModel)
    {
        var state = _pending ??= Capture();
        Data = ContextData.Empty;
        _states.Clear();
        _selection.Rows.Clear();
        _sync.RaiseSelectionCounts();
        Categories.RebuildQuick(documents);
        Rows.ReplaceAll(_tree.ShowQuickCounts(documents, state));
    }

    internal Task LoadAsync(ContextData data, CancellationToken cancellationToken)
    {
        var state = _pending ?? Capture();
        Data = data;
        _tree.Token = cancellationToken;
        return BuildingAsync(async () =>
        {
            _tree.Clear();
            await _rows.ReadBackAsync();
            await Categories.ReadCheckedAsync(data, cancellationToken);
            Categories.Rebuild(data);
            Pick.Refresh();
            await BuildTreeAsync(state);
        });
    }

    private async Task BuildingAsync(Func<Task> build)
    {
        _building++;
        try
        {
            await build();
        }
        finally
        {
            _building--;
            if (_building == 0 && _repaintPending && !_tree.Token.IsCancellationRequested)
            {
                OnVisibilityChanged();
            }
        }
    }

    private Task BuildTreeAsync(ExplorerState state)
    {
        return BuildingAsync(() => BuildRowsAsync(state));
    }

    private async Task BuildRowsAsync(ExplorerState state)
    {
        _selection.NextVersion();
        _selection.Rows.Clear();
        _tree.Clear();
        _rows.ComputeStates();
        var generation = _tree.Build();
        try
        {
            await RestoreAsync(state, generation);
            await _sync.CompleteLoadAsync();
        }
        catch (StaleBuildException)
        {
        }
    }

    private async Task RestoreAsync(ExplorerState state, int generation)
    {
        try
        {
            foreach (var root in _tree.Roots.Where(root => state.Expanded.Contains(root.Key) || !state.Roots.Contains(root.Key)).ToList())
            {
                await _tree.RestoreAsync(root, state.Expanded);
                _tree.EnsureCurrent(generation);
            }
        }
        finally
        {
            _tree.EndBuild(generation);
        }

        _pending = null;
        var text = SearchText.Trim();
        if (text.Length > 0)
        {
            await _search.CollectAsync(text);
            _tree.EnsureCurrent(generation);
        }
    }

    internal Task ApplyHostSelectionAsync(IReadOnlyCollection<ElementRef> incoming)
    {
        return _sync.ApplyHostSelectionAsync(incoming);
    }

    private Task Run(Func<Task> work)
    {
        _report(null);
        return _runtime.RunAsync(work);
    }

    [RelayCommand]
    private Task ToggleExpandAsync(TreeRow? row)
    {
        return Run(() => ToggleRowAsync(row));
    }

    [RelayCommand]
    private Task OpenRowAsync(TreeRow? row)
    {
        if (row is not { Kind: TreeRowKind.Instance })
        {
            return row is { HasChildren: true } ? ToggleExpandAsync(row) : Task.CompletedTask;
        }

        if (_selection.Rows.Count != 1 || !_selection.Rows.Contains(row))
        {
            SetSelectedRows([row]);
            RowSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        _report(null);
        _targets.Open(IsSelectionSyncing, _tree.Token);
        return _menu.SelectAndZoomAsync();
    }

    [RelayCommand]
    private Task LoadMoreAsync(TreeRow? more)
    {
        return Run(() => _rows.LoadMoreAsync(more));
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ToggleHiddenAsync(TreeRow? row)
    {
        if (_hiding)
        {
            return;
        }

        _hiding = true;
        try
        {
            await Run(() => ToggleRowHiddenAsync(row));
        }
        finally
        {
            _hiding = false;
        }
    }

    [RelayCommand]
    private Task ClearSelectionAsync()
    {
        return Run(_sync.ClearAllAsync);
    }

    [RelayCommand]
    private Task ExpandAllAsync()
    {
        return Run(ExpandTreeAsync);
    }

    [RelayCommand]
    private void CollapseAll()
    {
        _report(null);
        CollapseTree();
    }

    [RelayCommand]
    private Task RevealAllAsync()
    {
        return Run(_sync.RevealAllAsync);
    }

    [RelayCommand]
    private void ShowInExplorer()
    {
        _sync.ShowInExplorer();
    }

    private async Task ToggleRowAsync(TreeRow? row)
    {
        if (row is null)
        {
            return;
        }

        if (row.IsExpanded)
        {
            Collapse(row);
            return;
        }

        await ExpandAsync(row);
    }

    private async Task ToggleRowHiddenAsync(TreeRow? row)
    {
        if (row is null || row.HideState is HideState.None or HideState.HiddenByCategory)
        {
            return;
        }

        var action = row.HideState == HideState.Hidden ? VisibilityAction.Show : VisibilityAction.Hide;
        _report(await _visibility.RunAsync(VisibilityRequest.ForRows(action, _targets.TargetsFor(row))));
    }

    private async Task ApplySearchAsync()
    {
        await _search.CollectAsync(SearchText.Trim());
        await _rows.ReadBackAsync();
        _rows.Publish();
    }

    private void OnVisibilityChanged()
    {
        _repaintPending = _building > 0;
        if (_repaintPending)
        {
            return;
        }

        Categories.Rebuild(Data);
        _rows.ComputeStates();
        _rows.Publish();
    }

    private void OnChecksChanged()
    {
        Pick.Refresh();
        Pick.Clear();
        var state = _pending ?? Capture();
        _ = Run(() => BuildTreeAsync(state));
    }

    private void UpdateDimmed()
    {
        var dimmed = _selection.Rows
            .Where(row => row is { Kind: TreeRowKind.Instance, Document: not null }
                          && (row.IsDimmed || row.HideState == HideState.Hidden))
            .Take(2)
            .ToList();
        SelectedDimmedElement = dimmed.Count == 1 ? new ElementRef(dimmed[0].Document!.Value, dimmed[0].Id) : null;
    }
}
