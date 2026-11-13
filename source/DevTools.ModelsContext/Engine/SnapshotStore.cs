using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;

namespace DevTools.ModelsContext.Engine;

public sealed class SnapshotStore(IModelSource source)
{
    private readonly Dictionary<DocumentKey, DocumentInfo> _documents = [];
    private readonly object _gate = new();
    private readonly Dictionary<SourceId, Task<DocumentSnapshot>> _snapshots = [];

    public IReadOnlyList<DocumentInfo> Documents
    {
        get
        {
            lock (_gate)
            {
                return _documents.Values.ToList();
            }
        }
    }

    public void SetDocuments(IEnumerable<DocumentInfo> documents)
    {
        lock (_gate)
        {
            _documents.Clear();
            foreach (var document in documents)
            {
                _documents[document.Key] = document;
            }

            var live = _documents.Values.Select(document => document.Source).ToHashSet();
            foreach (var stale in _snapshots.Keys.Where(id => !live.Contains(id)).ToList())
            {
                _snapshots.Remove(stale);
            }
        }
    }

    public DocumentInfo? Find(DocumentKey key)
    {
        lock (_gate)
        {
            return _documents.TryGetValue(key, out var info) ? info : null;
        }
    }

    public Task<DocumentSnapshot> GetAsync(
        DocumentKey key,
        IProgress<SnapshotProgress>? progress,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_documents.TryGetValue(key, out var info) || !info.IsLoaded)
            {
                throw new InvalidOperationException($"Document {key} is not loaded.");
            }

            if (_snapshots.TryGetValue(info.Source, out var task) && !IsUnusable(task))
            {
                return task;
            }

            task = source.BuildSnapshotAsync(info.Source, progress, cancellationToken);
            _snapshots[info.Source] = task;
            return task;
        }
    }

    public DocumentSnapshot? TryGet(DocumentKey key)
    {
        lock (_gate)
        {
            if (!_documents.TryGetValue(key, out var info)
                || !_snapshots.TryGetValue(info.Source, out var task)
                || task.Status != TaskStatus.RanToCompletion)
            {
                return null;
            }

            return task.Result;
        }
    }

    public DeltaOutcome Apply(ElementDelta delta)
    {
        DocumentSnapshot snapshot;
        lock (_gate)
        {
            if (!_snapshots.TryGetValue(delta.Source, out var task) || task.Status != TaskStatus.RanToCompletion)
            {
                return DeltaOutcome.Ignored;
            }

            snapshot = task.Result;
        }

        return DeltaApplier.Apply(snapshot, delta);
    }

    public void Drop(SourceId id)
    {
        lock (_gate)
        {
            _snapshots.Remove(id);
        }
    }

    private static bool IsUnusable(Task<DocumentSnapshot> task)
    {
        return task.IsFaulted || task.IsCanceled || (task.Status == TaskStatus.RanToCompletion && task.Result.IsDirty);
    }
}
