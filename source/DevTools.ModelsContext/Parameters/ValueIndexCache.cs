using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.ModelsContext.Parameters;

public sealed class ValueIndexCache(
    IFilterEvaluator evaluator,
    int capacity = ValueIndexCache.DefaultCapacity,
    ILogger? logger = null)
{
    private readonly ILogger _logger = logger ?? NullLogger.Instance;
    public const int DefaultCapacity = 8;
    public const int MaxValues = 500;

    private readonly Dictionary<Key, LinkedListNode<Entry>> _entries = [];
    private readonly object _gate = new();
    private readonly LinkedList<Entry> _order = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public Task<ValueIndex> GetAsync(
        SourceId source,
        ParameterKey parameter,
        IReadOnlyCollection<long> groups,
        CancellationToken cancellationToken)
    {
        var key = KeyOf(source, parameter, groups);
        Task<ValueIndex> task;
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                return node.Value.Index;
            }

            task = BuildAsync(source, parameter, groups, cancellationToken);
            _entries.Add(key, _order.AddFirst(new Entry(key, task)));
            while (_entries.Count > capacity)
            {
                var last = _order.Last!;
                _order.RemoveLast();
                _entries.Remove(last.Value.Key);
            }
        }

        _ = task.ContinueWith(
            _ => Forget(key, task),
            CancellationToken.None,
            TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return task;
    }

    public bool Contains(SourceId source, ParameterKey parameter, IReadOnlyCollection<long> groups)
    {
        lock (_gate)
        {
            return _entries.ContainsKey(KeyOf(source, parameter, groups));
        }
    }

    public void Invalidate(SourceId source)
    {
        lock (_gate)
        {
            foreach (var key in _entries.Keys.Where(key => key.Source == source).ToList())
            {
                _order.Remove(_entries[key]);
                _entries.Remove(key);
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _order.Clear();
        }
    }

    public static ValueIndex Summarize(ParameterValueCounts counts)
    {
        var hits = counts.Values.Values.Sum();
        var ordered = counts.Values
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxValues + 1)
            .Select(pair => new ValueCount(pair.Key, pair.Value, StoredOf(counts, pair.Key)))
            .ToList();
        var hasMore = ordered.Count > MaxValues;
        if (hasMore)
        {
            ordered.RemoveAt(MaxValues);
        }

        return new ValueIndex(ordered, hasMore, Math.Max(0, counts.Total - hits));
    }

    private static NumberRange? StoredOf(ParameterValueCounts counts, string value)
    {
        return counts.Numbers is { } numbers && numbers.TryGetValue(value, out var range) ? range : null;
    }

    private static Key KeyOf(SourceId source, ParameterKey parameter, IReadOnlyCollection<long> groups)
    {
        return new Key(source, parameter, string.Join(",", groups.Distinct().OrderBy(id => id)));
    }

    private async Task<ValueIndex> BuildAsync(
        SourceId source,
        ParameterKey parameter,
        IReadOnlyCollection<long> groups,
        CancellationToken cancellationToken)
    {
        var timer = TraceTimer.Start(_logger);
        var counts = await evaluator.CountValuesAsync(source, parameter, groups, cancellationToken)
            .ConfigureAwait(false);
        if (timer.IsEnabled)
        {
            _logger.ValuesCounted(source.Label, parameter.Name, counts.Values.Count, counts.Total, timer.ElapsedMs);
        }

        return Summarize(counts);
    }

    private void Forget(Key key, Task<ValueIndex> task)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var node) && ReferenceEquals(node.Value.Index, task))
            {
                _order.Remove(node);
                _entries.Remove(key);
            }
        }
    }

    private readonly record struct Key(SourceId Source, ParameterKey Parameter, string Groups);

    private sealed record Entry(Key Key, Task<ValueIndex> Index);
}
