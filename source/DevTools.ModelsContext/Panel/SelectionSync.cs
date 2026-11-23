using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Scope;

namespace DevTools.ModelsContext.Panel;

internal sealed class SelectionSync : IDisposable
{
    private readonly Debouncer _debouncer;
    private readonly object _gate = new();
    private readonly ModelContextViewModel _owner;
    private readonly PanelRuntime _runtime;
    private readonly SessionSwitcher _sessions;
    private IReadOnlyCollection<ElementRef>? _incoming;

    public SelectionSync(PanelRuntime runtime, SessionSwitcher sessions, ModelContextViewModel owner)
    {
        _runtime = runtime;
        _sessions = sessions;
        _owner = owner;
        _debouncer = new Debouncer(runtime.Scheduler, ModelContextViewModel.EventDelay,
            () => _ = runtime.RunAsync(SettleAsync));
        runtime.Services.Events.SelectionChanged += OnSelectionChanged;
    }

    public void Dispose()
    {
        _runtime.Services.Events.SelectionChanged -= OnSelectionChanged;
        _debouncer.Dispose();
    }

    public void Cancel()
    {
        _debouncer.Cancel();
        lock (_gate)
        {
            _incoming = null;
        }
    }

    public async Task ResyncAsync()
    {
        if (_sessions.Active is not { } session || !_owner.IsPaneVisible)
        {
            return;
        }

        var ticket = session.Peek();
        var selection = await _runtime.Services.Selection.GetSelectionAsync(ticket.Token);
        if (_owner.IsCurrent(ticket))
        {
            await _owner.Explorer.ApplyHostSelectionAsync(selection);
        }
    }

    private void OnSelectionChanged(object? sender, IReadOnlyCollection<ElementRef> selection)
    {
        if (!_owner.IsPaneVisible)
        {
            return;
        }

        lock (_gate)
        {
            _incoming = selection;
        }

        _debouncer.Trigger();
    }

    private async Task SettleAsync()
    {
        IReadOnlyCollection<ElementRef>? incoming;
        lock (_gate)
        {
            incoming = _incoming;
            _incoming = null;
        }

        if (incoming is null || !Accept(incoming))
        {
            return;
        }

        if (_owner.ContextBar.Scope == ScopeKind.Selection)
        {
            await _owner.RefreshAsync();
        }

        if (_owner.SelectedTab == PanelTab.Explorer)
        {
            await ApplyToExplorerAsync(incoming);
        }
    }

    private bool Accept(IReadOnlyCollection<ElementRef> incoming)
    {
        var awaitingEcho = _runtime.Guard.IsPending;
        if (_runtime.Guard.ShouldDrop(incoming))
        {
            return false;
        }

        if (awaitingEcho)
        {
            _runtime.Logger.SelectionMismatch(incoming.Count);
        }

        return true;
    }

    private async Task ApplyToExplorerAsync(IReadOnlyCollection<ElementRef> incoming)
    {
        var timer = TraceTimer.Start(_runtime.Logger);
        await _owner.Explorer.ApplyHostSelectionAsync(incoming);
        if (timer.IsEnabled)
        {
            _runtime.Logger.SelectionReceived(incoming.Count, timer.ElapsedMs);
        }
    }
}
