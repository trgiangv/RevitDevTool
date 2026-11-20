using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI.Events;
using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using Nice3point.Revit.Extensions;
using RevitDevTool.Core;
using System.Windows.Threading;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class RevitModelEvents(ScopeCache scopes, ParameterCache parameterCache, Dispatcher dispatcher)
    : IRevitModelEvents
{
    private UIControlledApplication? _application;
    private readonly Dictionary<string, TrackedIds> _keys = new(StringComparer.Ordinal);
    private string? _activeModel;
    private bool _isBusy = true;
    private bool _subscribed;

    public bool IsActive { get; set; }

    public bool IsBusy => _isBusy;

    public event EventHandler? DocumentsChanged;

    public event EventHandler<ElementDelta>? ElementsChanged;

    public event EventHandler<IReadOnlyCollection<ElementRef>>? SelectionChanged;

    public event EventHandler<ViewInfo>? ViewActivated;

    public event EventHandler<DocumentKey>? DocumentClosing;

    public event EventHandler<DocumentKey>? ActiveModelChanged;

    public event EventHandler<DocumentKey>? ModelSynced;

    public event EventHandler<LinkStatusChange>? LinkStatusChanged;

    public event EventHandler<bool>? BusyChanged;

    public event EventHandler<SourceId>? ParameterDefinitionsChanged;

    public void Subscribe(UIControlledApplication application)
    {
        if (_subscribed)
        {
            return;
        }

        _application = application;
        var controlled = application.ControlledApplication;
        controlled.DocumentChanged += OnDocumentChanged;
        controlled.DocumentClosing += OnDocumentClosing;
        controlled.DocumentClosed += OnDocumentClosed;
        controlled.DocumentOpened += OnDocumentOpened;
        controlled.DocumentCreated += OnDocumentCreated;
        controlled.DocumentSynchronizedWithCentral += OnSynchronized;
        controlled.DocumentReloadedLatest += OnReloadedLatest;
        application.ViewActivated += OnViewActivated;
        application.SelectionChanged += OnSelectionChanged;
        _subscribed = true;
    }

    public void Unsubscribe()
    {
        if (!_subscribed || _application is not { } application)
        {
            return;
        }

        var controlled = application.ControlledApplication;
        controlled.DocumentChanged -= OnDocumentChanged;
        controlled.DocumentClosing -= OnDocumentClosing;
        controlled.DocumentClosed -= OnDocumentClosed;
        controlled.DocumentOpened -= OnDocumentOpened;
        controlled.DocumentCreated -= OnDocumentCreated;
        controlled.DocumentSynchronizedWithCentral -= OnSynchronized;
        controlled.DocumentReloadedLatest -= OnReloadedLatest;
        application.ViewActivated -= OnViewActivated;
        application.SelectionChanged -= OnSelectionChanged;
        _keys.Clear();
        _subscribed = false;
    }

    private void Post(string name, Action raise)
    {
        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            try
            {
                raise();
            }
            catch (Exception exception)
            {
                RevitProfiler.Logger.Swallowed(exception, "Panel event", name);
            }
        }));
    }

    private void SetBusy(bool busy)
    {
        if (_isBusy == busy)
        {
            return;
        }

        _isBusy = busy;
        Post(nameof(BusyChanged), () => BusyChanged?.Invoke(this, busy));
    }

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        var timer = TraceTimer.Start(RevitProfiler.Logger);
        try
        {
            var model = e.GetDocument();
            if (model.IsLinked)
            {
                return;
            }

            scopes.Clear();
            var reader = new DocumentChangeReader(model, e.GetAddedElementIds(), e.GetModifiedElementIds(),
                e.GetDeletedElementIds());
            var report = reader.Read();
            var source = DocumentRegistry.SourceOf(model);
            var outcome = ChangeClassifier.Classify(report, () => KeysOf(model));
            Apply(model, source, outcome, reader);
            var delta = reader.Delta(source, report,
                e.Operation is UndoOperation.TransactionUndone or UndoOperation.TransactionRedone);
            Post(nameof(ElementsChanged), () => ElementsChanged?.Invoke(this, delta));
            if (timer.IsEnabled)
            {
                RevitProfiler.Logger.DocumentChangeHandled(model.Title, report.Added, report.Modified,
                    report.Deleted.Count, report.IsBulk, timer.ElapsedMs);
            }
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Document changed", e.Operation);
        }
    }

    private void Apply(Document model, SourceId source, ChangeOutcome outcome, DocumentChangeReader reader)
    {
        if (outcome.IsBulk)
        {
            _keys.Remove(DocumentRegistry.ModelIdOf(model));
            DocumentChangeReader.InvalidateLinks(model, parameterCache);
        }

        var statuses = outcome.LinkTypes
            .SelectMany(type => DocumentChangeReader.LinkStatusesOf(model, type, parameterCache))
            .ToList();
        parameterCache.Invalidate(source, outcome.Drop, reader.Modified, reader.Keeps);
        if (outcome.DefinitionsChanged)
        {
            Post(nameof(ParameterDefinitionsChanged), () => ParameterDefinitionsChanged?.Invoke(this, source));
        }

        foreach (var status in statuses)
        {
            Post(nameof(LinkStatusChanged), () => LinkStatusChanged?.Invoke(this, status));
        }

        if (outcome.DocumentsChanged)
        {
            Post(nameof(DocumentsChanged), () => DocumentsChanged?.Invoke(this, EventArgs.Empty));
        }
    }

    private TrackedIds KeysOf(Document model)
    {
        var modelId = DocumentRegistry.ModelIdOf(model);
        if (_keys.TryGetValue(modelId, out var keys))
        {
            return keys;
        }

        keys = DocumentChangeReader.TrackedOf(model);
        _keys.Add(modelId, keys);
        return keys;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (!IsActive || LinkedHideShow.IsRunning)
            {
                return;
            }

            var selection = RevitSelectionPort.ToSelection(e.GetDocument(), e.GetReferences(), e.GetSelectedElements());
            Post(nameof(SelectionChanged), () => SelectionChanged?.Invoke(this, selection));
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Selection changed", _activeModel);
        }
    }

    private void OnViewActivated(object? sender, ViewActivatedEventArgs e)
    {
        try
        {
            scopes.Clear();
            SetBusy(false);
            var model = e.Document;
            var view = RevitViewActions.ViewInfoOf(model, e.CurrentActiveView);
            var modelId = DocumentRegistry.ModelIdOf(model);
            if (!string.Equals(_activeModel, modelId, StringComparison.Ordinal))
            {
                _activeModel = modelId;
                var key = view.Model;
                Post(nameof(ActiveModelChanged), () => ActiveModelChanged?.Invoke(this, key));
            }

            Post(nameof(ViewActivated), () => ViewActivated?.Invoke(this, view));
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "View activated", _activeModel);
        }
    }

    private void OnDocumentClosing(object? sender, DocumentClosingEventArgs e)
    {
        try
        {
            var model = e.Document;
            if (model.IsLinked)
            {
                return;
            }

            var modelId = DocumentRegistry.ModelIdOf(model);
            _keys.Remove(modelId);
            scopes.Clear();
            if (string.Equals(_activeModel, modelId, StringComparison.Ordinal))
            {
                _activeModel = null;
            }

            var key = DocumentKey.Current(modelId);
            Post(nameof(DocumentClosing), () => DocumentClosing?.Invoke(this, key));
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Document closing", _activeModel);
        }
    }

    private void OnDocumentClosed(object? sender, DocumentClosedEventArgs e)
    {
        try
        {
            var open = false;
            foreach (Document document in RevitContext.Application.Documents)
            {
                open |= !document.IsLinked;
            }

            SetBusy(!open);
            Post(nameof(DocumentsChanged), () => DocumentsChanged?.Invoke(this, EventArgs.Empty));
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Document closed", _activeModel);
        }
    }

    private void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e)
    {
        Post(nameof(DocumentsChanged), () => DocumentsChanged?.Invoke(this, EventArgs.Empty));
    }

    private void OnDocumentCreated(object? sender, DocumentCreatedEventArgs e)
    {
        Post(nameof(DocumentsChanged), () => DocumentsChanged?.Invoke(this, EventArgs.Empty));
    }

    private void OnSynchronized(object? sender, DocumentSynchronizedWithCentralEventArgs e)
    {
        RaiseSynced(e.Document);
    }

    private void OnReloadedLatest(object? sender, DocumentReloadedLatestEventArgs e)
    {
        RaiseSynced(e.Document);
    }

    private void RaiseSynced(Document model)
    {
        try
        {
            if (model.IsLinked)
            {
                return;
            }

            var key = DocumentRegistry.KeyOf(model);
            Post(nameof(ModelSynced), () => ModelSynced?.Invoke(this, key));
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Model synced", _activeModel);
        }
    }
}
