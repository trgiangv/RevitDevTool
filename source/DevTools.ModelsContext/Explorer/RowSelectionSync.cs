using CommunityToolkit.Mvvm.ComponentModel;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;
using System.Globalization;

namespace DevTools.ModelsContext.Explorer;

internal sealed partial class RowSelectionSync : ObservableObject, IDisposable
{
    private readonly ContextBarViewModel _bar;
    private readonly TreeReveal _reveal;
    private readonly ExplorerRows _rows;
    private readonly PanelRuntime _runtime;
    private readonly ExplorerSelection _selection;
    private readonly Debouncer _sendDebouncer;
    private readonly ExplorerTree _tree;
    private bool _isSelectionSyncOn = true;
    private bool _revealPending;
    private IReadOnlyList<ElementRef>? _rowElements;
    private int _sendsInFlight;

    public RowSelectionSync(
        PanelRuntime runtime,
        ContextBarViewModel bar,
        ExplorerTree tree,
        TreeReveal reveal,
        ExplorerRows rows,
        ExplorerSelection selection)
    {
        _runtime = runtime;
        _bar = bar;
        _tree = tree;
        _reveal = reveal;
        _rows = rows;
        _selection = selection;
        _sendDebouncer = new Debouncer(runtime.Scheduler, ExplorerTabViewModel.SelectionSyncDelay,
            () => _ = _runtime.RunAsync(SendAsync));
    }

    public event EventHandler? RowSelectionChanged;

    public event EventHandler<TreeRow>? ScrollRequested;

    public bool IsSelectionSyncOn
    {
        get => _isSelectionSyncOn;
        set
        {
            if (!SetProperty(ref _isSelectionSyncOn, value))
            {
                return;
            }

            IsSelectionSyncPaused = false;
            OnPropertyChanged(nameof(IsSelectionSyncing));
            if (value)
            {
                _sendDebouncer.Trigger();
            }
        }
    }

    public bool IsSelectionSyncVisible => _bar.Scope != ScopeKind.Selection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectionSyncing))]
    public partial bool IsSelectionSyncPaused { get; private set; }

    public bool IsSelectionSyncing => IsSelectionSyncOn && IsSelectionSyncVisible && !IsSelectionSyncPaused;

    [ObservableProperty]
    public partial string? SelectionSyncHint { get; private set; }

    public int SelectedCount => RowElements.Count;

    public bool HasSelection => SelectedCount > 0 || _selection.Elements.Count > 0;

    public string StatusText => string.Format(CultureInfo.CurrentCulture, "{0:N0} selected", SelectedCount);

    public bool CanShowInExplorer =>
        IsSelectionSyncVisible && _selection.Elements.Any(element => _runtime.Data.Find(element.Document) is null);

    public bool IsLocalSelectionPending => _sendDebouncer.IsPending || _sendsInFlight > 0;

    private bool IsSyncActive => IsSelectionSyncOn && IsSelectionSyncVisible;

    private IReadOnlyList<ElementRef> RowElements => _rowElements ??= _runtime.Data.ElementsOf(_selection.Rows);

    public void Dispose()
    {
        _sendDebouncer.Dispose();
    }

    public void SetSelectedRows(IEnumerable<TreeRow> rows)
    {
        _selection.NextVersion();
        _selection.ReplaceRows(rows);
        RaiseSelectionCounts();
        if (IsSyncActive && !_runtime.IsBusy)
        {
            _tree.ClearBadges();
            _sendDebouncer.Trigger();
        }
    }

    public void Reset(bool isSelectionSyncOn)
    {
        _selection.NextVersion();
        _selection.Rows.Clear();
        _selection.Elements = [];
        _revealPending = false;
        SelectionSyncHint = null;
        IsSelectionSyncPaused = false;
        if (_isSelectionSyncOn != isSelectionSyncOn)
        {
            _isSelectionSyncOn = isSelectionSyncOn;
            OnPropertyChanged(nameof(IsSelectionSyncOn));
            OnPropertyChanged(nameof(IsSelectionSyncing));
        }

        _sendDebouncer.Cancel();
    }

    public async Task CompleteLoadAsync()
    {
        if (_revealPending)
        {
            _revealPending = false;
            _selection.Rows.UnionWith(await _reveal.RevealAsync(_selection.Elements,
                ExplorerTabViewModel.OpenPathLimit));
        }

        await _rows.ReadHiddenAsync();
        RaiseSelectionCounts();
        OnPropertyChanged(nameof(IsSelectionSyncVisible));
        OnPropertyChanged(nameof(IsSelectionSyncing));
        OnPropertyChanged(nameof(CanShowInExplorer));
        _rows.Publish();
        RaiseRowSelectionChanged();
    }

    public async Task ApplyHostSelectionAsync(IReadOnlyCollection<ElementRef> incoming)
    {
        if (IsLocalSelectionPending)
        {
            return;
        }

        var version = _selection.NextVersion();
        _selection.Elements = [.. incoming];
        SelectionSyncHint = null;
        RaiseSelectionCounts();
        _rows.ComputeSelected();
        if (!IsSyncActive || PauseIfTooMany(_selection.Elements.Count))
        {
            _rows.Publish();
            return;
        }

        var revealed = await RevealHostSelectionAsync();
        if (version != _selection.Version)
        {
            return;
        }

        await ShowRevealedAsync(revealed);
        if (_rows.Items.FirstOrDefault(_selection.Rows.Contains) is { } first)
        {
            ScrollRequested?.Invoke(this, first);
        }
    }

    public async Task ClearAllAsync()
    {
        _selection.Rows.Clear();
        _selection.Elements.Clear();
        SelectionSyncHint = null;
        await _runtime.SelectAsync([], _tree.Token);
        RaiseSelectionCounts();
        _rows.ComputeSelected();
        _rows.Publish();
        RaiseRowSelectionChanged();
    }

    public async Task RevealAllAsync()
    {
        SelectionSyncHint = null;
        var version = _selection.NextVersion();
        var revealed = await _reveal.RevealAsync(_selection.Elements, int.MaxValue);
        if (version == _selection.Version)
        {
            await ShowRevealedAsync(revealed);
        }
    }

    public void ShowInExplorer()
    {
        var missing = _selection.Elements.Select(element => element.Document)
            .Where(document => _runtime.Data.Find(document) is null)
            .Distinct()
            .ToList();
        if (missing.Count == 0)
        {
            return;
        }

        _revealPending = true;
        _bar.Tick(missing);
    }

    public void RaiseSelectionCounts()
    {
        _rowElements = null;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CanShowInExplorer));
    }

    public void RaiseRowSelectionChanged()
    {
        RowSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task SendAsync()
    {
        if (!IsSyncActive || _runtime.IsBusy)
        {
            return;
        }

        var elements = RowElements;
        if (PauseIfTooMany(elements.Count))
        {
            return;
        }

        var version = _selection.Version;
        _sendsInFlight++;
        try
        {
            await _runtime.SelectAsync(elements, _tree.Token);
        }
        finally
        {
            _sendsInFlight--;
        }

        if (version != _selection.Version)
        {
            return;
        }

        _selection.Elements = [.. elements];
        SelectionSyncHint = null;
        RaiseSelectionCounts();
        _rows.ComputeSelected();
        _rows.Publish();
        RaiseRowSelectionChanged();
    }

    private bool PauseIfTooMany(int count)
    {
        IsSelectionSyncPaused = count > ExplorerTabViewModel.SelectionSyncPauseLimit;
        return IsSelectionSyncPaused;
    }

    private async Task<List<TreeRow>> RevealHostSelectionAsync()
    {
        if (_selection.Elements.Count <= ExplorerTabViewModel.OpenPathLimit)
        {
            return await _reveal.RevealAsync(_selection.Elements, ExplorerTabViewModel.OpenPathLimit);
        }

        SelectionSyncHint = ExplorerTabViewModel.TooManyHint;
        return [];
    }

    private async Task ShowRevealedAsync(List<TreeRow> revealed)
    {
        _selection.ReplaceRows(revealed);
        RaiseSelectionCounts();
        await _rows.ReadHiddenAsync();
        _rows.Publish();
        RaiseRowSelectionChanged();
    }
}
