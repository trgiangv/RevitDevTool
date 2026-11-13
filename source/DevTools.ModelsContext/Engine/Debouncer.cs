using DevTools.ModelsContext.Ports;

namespace DevTools.ModelsContext.Engine;

public sealed class Debouncer(IScheduler scheduler, TimeSpan delay, Action action) : IDisposable
{
    private readonly object _gate = new();
    private IDisposable? _pending;
    private int _generation;

    public bool IsPending
    {
        get
        {
            lock (_gate)
            {
                return _pending is not null;
            }
        }
    }

    public void Trigger()
    {
        lock (_gate)
        {
            _pending?.Dispose();
            var generation = ++_generation;
            _pending = scheduler.Schedule(delay, () => Fire(generation));
        }
    }

    public void Cancel()
    {
        lock (_gate)
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
        lock (_gate)
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
