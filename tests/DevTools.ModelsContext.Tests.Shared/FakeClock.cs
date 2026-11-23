using DevTools.ModelsContext.Panel;

namespace DevTools.ModelsContext.Tests.Shared;

public sealed class FakeClock : IClock, IScheduler
{
    private readonly List<Scheduled> _queue = [];
    private long _sequence;

    public DateTimeOffset Now { get; private set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public RecordingContext Context { get; } = new();

    SynchronizationContext IScheduler.Context => Context;

    public int PendingCount => _queue.Count;

    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        var item = new Scheduled(this, Now + delay, _sequence++, action);
        _queue.Add(item);
        return item;
    }

    public void Advance(TimeSpan by)
    {
        var target = Now + by;
        while (true)
        {
            var next = _queue
                .Where(item => item.Due <= target)
                .OrderBy(item => item.Due)
                .ThenBy(item => item.Sequence)
                .FirstOrDefault();
            if (next is null)
            {
                break;
            }

            _queue.Remove(next);
            Now = next.Due;
            next.Action();
        }

        Now = target;
    }

    public sealed class RecordingContext : SynchronizationContext
    {
        public int Posts { get; private set; }

        public override void Post(SendOrPostCallback d, object? state)
        {
            Posts++;
            d(state);
        }
    }

    private sealed class Scheduled(FakeClock owner, DateTimeOffset due, long sequence, Action action) : IDisposable
    {
        public DateTimeOffset Due { get; } = due;

        public long Sequence { get; } = sequence;

        public Action Action { get; } = action;

        public void Dispose()
        {
            owner._queue.Remove(this);
        }
    }
}
