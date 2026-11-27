using DevTools.ModelsContext.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.ModelsContext.Documents;

public sealed class SnapshotStore(IModelSource source, ILogger? logger = null)
{
    private readonly ILogger _logger = logger ?? NullLogger.Instance;
    private readonly Dictionary<DocumentKey, DocumentInfo> _documents = [];
    private readonly object _lock = new();
    private readonly Dictionary<SourceId, Task<DocumentSnapshot>> _snapshots = [];

    public IReadOnlyList<DocumentInfo> Documents
    {
        get
        {
            lock (_lock)
            {
                return _documents.Values.ToList();
            }
        }
    }

    public IReadOnlyList<SourceId> SetDocuments(IEnumerable<DocumentInfo> documents)
    {
        lock (_lock)
        {
            var list = documents.ToList();
            var models = list.Select(document => document.Key.ModelId).ToHashSet(StringComparer.Ordinal);
            foreach (var key in _documents.Keys.Where(key => models.Contains(key.ModelId)).ToList())
            {
                _documents.Remove(key);
            }

            foreach (var document in list)
            {
                _documents[document.Key] = document;
            }

            return DropUnused();
        }
    }

    public IReadOnlyList<SourceId> DropModel(string modelId)
    {
        lock (_lock)
        {
            foreach (var key in _documents.Keys.Where(key => key.ModelId == modelId).ToList())
            {
                _documents.Remove(key);
            }

            return DropUnused();
        }
    }

    public void Rename(ModelRename rename)
    {
        lock (_lock)
        {
            foreach (var pair in _documents.Where(pair => pair.Key.ModelId == rename.OldId).ToList())
            {
                _documents.Remove(pair.Key);
                _documents[rename.Map(pair.Key)] = rename.Map(pair.Value);
            }

            if (!_snapshots.TryGetValue(rename.OldSource, out var task))
            {
                return;
            }

            _snapshots.Remove(rename.OldSource);
            _snapshots[rename.NewSource] = task.ContinueWith(built =>
            {
                var snapshot = built.Result;
                snapshot.Rename(rename.NewSource);
                return snapshot;
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    public int UsersOf(SourceId id)
    {
        lock (_lock)
        {
            return _documents.Values
                .Where(document => document.Source == id)
                .Select(document => document.Key.ModelId)
                .Distinct(StringComparer.Ordinal)
                .Count();
        }
    }

    public bool Holds(SourceId id)
    {
        lock (_lock)
        {
            return _snapshots.ContainsKey(id);
        }
    }

    public DocumentInfo? Find(DocumentKey key)
    {
        lock (_lock)
        {
            return _documents.TryGetValue(key, out var info) ? info : null;
        }
    }

    public Task<DocumentSnapshot> GetAsync(
        DocumentKey key,
        IProgress<SnapshotProgress>? progress,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!_documents.TryGetValue(key, out var info) || !info.IsLoaded)
            {
                throw new InvalidOperationException($"Document {key} is not loaded.");
            }

            if (_snapshots.TryGetValue(info.Source, out var task) && !IsUnusable(task))
            {
                return task;
            }

            task = _logger.IsEnabled(LogLevel.Trace)
                ? TimedBuildAsync(info.Source, progress, cancellationToken)
                : source.BuildSnapshotAsync(info.Source, progress, cancellationToken);
            _snapshots[info.Source] = task;
            return task;
        }
    }

    public DocumentSnapshot? TryGet(DocumentKey key)
    {
        lock (_lock)
        {
            return _documents.TryGetValue(key, out var info) ? Completed(info.Source) : null;
        }
    }

    public bool IsReady(DocumentKey key)
    {
        return TryGet(key) is { IsDirty: false };
    }

    public DeltaOutcome Apply(ElementDelta delta)
    {
        DocumentSnapshot? snapshot;
        lock (_lock)
        {
            snapshot = Completed(delta.Source);
        }

        return snapshot is null ? DeltaOutcome.Ignored : DeltaApplier.Apply(snapshot, delta);
    }

    public void MarkDirty(SourceId id)
    {
        lock (_lock)
        {
            Completed(id)?.MarkDirty();
        }
    }

    public void Drop(SourceId id)
    {
        lock (_lock)
        {
            _snapshots.Remove(id);
        }
    }

    public async Task<ReloadOutcome> ReloadAsync(SourceId id, CancellationToken cancellationToken)
    {
        var timer = TraceTimer.Start(_logger);
        var outcome = await ReloadCoreAsync(id, cancellationToken).ConfigureAwait(false);
        if (timer.IsEnabled)
        {
            _logger.SnapshotReloaded(id.Label, outcome, timer.ElapsedMs);
        }

        return outcome;
    }

    private async Task<DocumentSnapshot> TimedBuildAsync(
        SourceId id,
        IProgress<SnapshotProgress>? progress,
        CancellationToken cancellationToken)
    {
        var timer = TraceTimer.Start(_logger);
        var snapshot = await source.BuildSnapshotAsync(id, progress, cancellationToken).ConfigureAwait(false);
        _logger.SnapshotBuilt(id.Label, snapshot.ElementCount, timer.ElapsedMs);
        return snapshot;
    }

    private async Task<ReloadOutcome> ReloadCoreAsync(SourceId id, CancellationToken cancellationToken)
    {
        DocumentSnapshot? snapshot;
        lock (_lock)
        {
            snapshot = Completed(id);
        }

        if (snapshot is null || snapshot.IsDirty || snapshot.Stamp is not { } stamp)
        {
            return Rebuild(snapshot);
        }

        var current = await source.GetVersionAsync(id, cancellationToken).ConfigureAwait(false);
        if (current is not { } now)
        {
            return Rebuild(snapshot);
        }

        if (VersionRule.IsSame(stamp, now))
        {
            return ReloadOutcome.Kept;
        }

        var delta = await source.GetChangesSinceAsync(id, stamp, cancellationToken).ConfigureAwait(false);
        if (delta is null
            || VersionRule.IsTooLarge(delta, snapshot.ElementCount)
            || DeltaApplier.Apply(snapshot, delta, int.MaxValue) != DeltaOutcome.Applied)
        {
            return Rebuild(snapshot);
        }

        snapshot.SetStamp(now);
        return ReloadOutcome.Patched;
    }

    public async Task<bool> VerifyAsync(SourceId id, CancellationToken cancellationToken)
    {
        DocumentSnapshot? snapshot;
        lock (_lock)
        {
            snapshot = Completed(id);
        }

        if (snapshot is null || snapshot.IsDirty)
        {
            return true;
        }

        var current = await source.GetVersionAsync(id, cancellationToken).ConfigureAwait(false);
        if (current is not { } now || VersionRule.IsSame(snapshot.Stamp, now))
        {
            return true;
        }

        var delta = snapshot.Stamp is { } stamp
            ? await source.GetChangesSinceAsync(id, stamp, cancellationToken).ConfigureAwait(false)
            : null;
        if (delta is null || !VersionRule.IsReflected(snapshot, delta))
        {
            snapshot.MarkDirty();
            return false;
        }

        snapshot.SetStamp(now);
        return true;
    }

    private static ReloadOutcome Rebuild(DocumentSnapshot? snapshot)
    {
        snapshot?.MarkDirty();
        return ReloadOutcome.Rebuilt;
    }

    private static bool IsUnusable(Task<DocumentSnapshot> task)
    {
        return task.IsFaulted || task.IsCanceled || (task.Status == TaskStatus.RanToCompletion && task.Result.IsDirty);
    }

    private DocumentSnapshot? Completed(SourceId id)
    {
        return _snapshots.TryGetValue(id, out var task) && task.Status == TaskStatus.RanToCompletion
            ? task.Result
            : null;
    }

    private List<SourceId> DropUnused()
    {
        var live = _documents.Values.Select(document => document.Source).ToHashSet();
        var stale = _snapshots.Keys.Where(id => !live.Contains(id)).ToList();
        foreach (var id in stale)
        {
            _snapshots.Remove(id);
        }

        return stale;
    }
}
