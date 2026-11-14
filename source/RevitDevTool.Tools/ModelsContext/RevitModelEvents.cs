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

internal sealed class RevitModelEvents(UIControlledApplication application, ScopeCache scopes, Dispatcher dispatcher)
    : IModelEvents
{
    private readonly Dictionary<string, ModelKeys> _keys = new(StringComparer.Ordinal);
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

    public void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

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
        if (!_subscribed)
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

    private void Post(Action raise)
    {
        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            try
            {
                raise();
            }
            catch (Exception exception)
            {
                RevitProfiler.Logger.Swallowed(exception, "Panel event", raise.Method.Name);
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
        Post(() => BusyChanged?.Invoke(this, busy));
    }

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        try
        {
            var model = e.GetDocument();
            if (model.IsLinked)
            {
                return;
            }

            scopes.Clear();
            var added = e.GetAddedElementIds();
            var modified = e.GetModifiedElementIds();
            var deleted = e.GetDeletedElementIds();
            var keys = KeysOf(model);
            ReportStructure(model, keys, added, modified, deleted);
            var delta = DeltaOf(model, added, modified, deleted,
                e.Operation is UndoOperation.TransactionUndone or UndoOperation.TransactionRedone);
            Post(() => ElementsChanged?.Invoke(this, delta));
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Document changed", e.Operation);
        }
    }

    private ElementDelta DeltaOf(
        Document model,
        ICollection<ElementId> added,
        ICollection<ElementId> modified,
        ICollection<ElementId> deleted,
        bool undoRedo)
    {
        var source = DocumentRegistry.SourceOf(model);
        var deletedIds = deleted.Select(id => id.ToLong()).ToList();
        if (added.Count + modified.Count + deleted.Count > DeltaApplier.DirtyThreshold)
        {
            deletedIds.AddRange(added.Select(id => id.ToLong()));
            deletedIds.AddRange(modified.Select(id => id.ToLong()));
            return new ElementDelta(source, [], [], [], deletedIds, undoRedo);
        }

        var addedReader = new ElementReader(model, []);
        foreach (var id in added)
        {
            if (model.GetElement(id) is { } element)
            {
                addedReader.Read(element);
            }
        }

        var modifiedReader = new ElementReader(model, []);
        foreach (var id in modified)
        {
            if (model.GetElement(id) is { } element)
            {
                modifiedReader.Read(element);
            }
        }

        var types = addedReader.Types.Concat(modifiedReader.Types)
            .GroupBy(type => type.TypeId)
            .Select(group => group.Last())
            .ToList();
        return new ElementDelta(source, types, addedReader.Elements, modifiedReader.Elements, deletedIds, undoRedo);
    }

    private void ReportStructure(
        Document model,
        ModelKeys keys,
        ICollection<ElementId> added,
        ICollection<ElementId> modified,
        ICollection<ElementId> deleted)
    {
        var source = DocumentRegistry.SourceOf(model);
        if (added.Count + modified.Count + deleted.Count > DeltaApplier.DirtyThreshold)
        {
            _keys.Remove(DocumentRegistry.ModelIdOf(model));
            Post(() => ParameterDefinitionsChanged?.Invoke(this, source));
            Post(() => DocumentsChanged?.Invoke(this, EventArgs.Empty));
            return;
        }

        var addedChange = TrackAdded(model, keys, added);
        var (statuses, parametersModified) = ReadModified(model, modified);
        var deletedChange = TrackDeleted(keys, deleted);
        if (addedChange.Parameters || parametersModified || deletedChange.Parameters)
        {
            Post(() => ParameterDefinitionsChanged?.Invoke(this, source));
        }

        foreach (var status in statuses)
        {
            Post(() => LinkStatusChanged?.Invoke(this, status));
        }

        if (addedChange.Documents || deletedChange.Documents)
        {
            Post(() => DocumentsChanged?.Invoke(this, EventArgs.Empty));
        }
    }

    private static (bool Documents, bool Parameters) TrackAdded(
        Document model,
        ModelKeys keys,
        ICollection<ElementId> added)
    {
        var documents = false;
        var parameters = false;
        foreach (var id in added)
        {
            switch (model.GetElement(id))
            {
                case RevitLinkInstance:
                    keys.Links.Add(id.ToLong());
                    documents = true;
                    break;
                case RevitLinkType:
                    documents = true;
                    break;
                case ParameterElement:
                    keys.Parameters.Add(id.ToLong());
                    parameters = true;
                    break;
            }
        }

        return (documents, parameters);
    }

    private static (List<LinkStatusChange> Statuses, bool Parameters) ReadModified(
        Document model,
        ICollection<ElementId> modified)
    {
        var statuses = new List<LinkStatusChange>();
        var parameters = false;
        foreach (var id in modified)
        {
            switch (model.GetElement(id))
            {
                case RevitLinkType type:
                    statuses.AddRange(LinkStatusesOf(model, type));
                    break;
                case ParameterElement:
                    parameters = true;
                    break;
            }
        }

        return (statuses, parameters);
    }

    private static IEnumerable<LinkStatusChange> LinkStatusesOf(Document model, RevitLinkType type)
    {
        return new FilteredElementCollector(model)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .Where(instance => instance.GetTypeId() == type.Id)
            .Select(instance => new LinkStatusChange(DocumentRegistry.KeyOf(model, instance),
                instance.GetLinkDocument() is null ? LinkStatus.Unloaded : LinkStatus.Reloaded));
    }

    private static (bool Documents, bool Parameters) TrackDeleted(ModelKeys keys, ICollection<ElementId> deleted)
    {
        var documents = false;
        var parameters = false;
        foreach (var id in deleted.Select(id => id.ToLong()))
        {
            documents |= keys.Links.Remove(id);
            parameters |= keys.Parameters.Remove(id);
        }

        return (documents, parameters);
    }

    private ModelKeys KeysOf(Document model)
    {
        var modelId = DocumentRegistry.ModelIdOf(model);
        if (_keys.TryGetValue(modelId, out var keys))
        {
            return keys;
        }

        keys = new ModelKeys(
            [.. new FilteredElementCollector(model).OfClass(typeof(RevitLinkInstance)).ToElementIds().Select(id => id.ToLong())],
            [.. new FilteredElementCollector(model).OfClass(typeof(ParameterElement)).ToElementIds().Select(id => id.ToLong())]);
        _keys.Add(modelId, keys);
        return keys;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (!IsActive)
            {
                return;
            }

            var model = e.GetDocument();
            var selection = new HashSet<ElementRef>(RevitSelectionPort.ToRefs(model, e.GetReferences()));
            var key = DocumentRegistry.KeyOf(model);
            selection.UnionWith(e.GetSelectedElements().Select(id => new ElementRef(key, id.ToLong())));
            Post(() => SelectionChanged?.Invoke(this, selection));
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
                Post(() => ActiveModelChanged?.Invoke(this, key));
            }

            Post(() => ViewActivated?.Invoke(this, view));
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
            Post(() => DocumentClosing?.Invoke(this, key));
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
            Post(() => DocumentsChanged?.Invoke(this, EventArgs.Empty));
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Document closed", _activeModel);
        }
    }

    private void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e)
    {
        Post(() => DocumentsChanged?.Invoke(this, EventArgs.Empty));
    }

    private void OnDocumentCreated(object? sender, DocumentCreatedEventArgs e)
    {
        Post(() => DocumentsChanged?.Invoke(this, EventArgs.Empty));
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
            Post(() => ModelSynced?.Invoke(this, key));
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Model synced", _activeModel);
        }
    }

    private sealed record ModelKeys(HashSet<long> Links, HashSet<long> Parameters);
}
