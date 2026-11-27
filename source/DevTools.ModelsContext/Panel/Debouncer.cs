namespace DevTools.ModelsContext.Panel;

public sealed class Debouncer(IScheduler scheduler, TimeSpan delay, Action action) : IDisposable
{
    private readonly object _lock = new();
    private IDisposable? _pending;
    private int _generation;

    public bool IsPending
    {
        get
        {
            lock (_lock)
            {
                return _pending is not null;
            }
        }
    }

    public void Trigger()
    {
        lock (_lock)
        {
            _pending?.Dispose();
            var generation = ++_generation;
            _pending = scheduler.Schedule(delay, () => Fire(generation));
        }
    }

    public void Cancel()
    {
        lock (_lock)
        {
            _pending?.Dispose();
            _pending = null;
            _generation++;
        }
    }

    public void Dispose()
    {
        Cancel();
    }

    private void Fire(int generation)
    {
        lock (_lock)
        {
            if (generation != _generation)
            {
                return;
            }

            _pending = null;
        }

        action();
    }
}
