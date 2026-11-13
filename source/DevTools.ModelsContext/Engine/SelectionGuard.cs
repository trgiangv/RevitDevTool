using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;

namespace DevTools.ModelsContext.Engine;

public sealed class SelectionGuard(IClock clock, TimeSpan window)
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private HashSet<ElementRef>? _sent;
    private DateTimeOffset _sentAt;

    public SelectionGuard(IClock clock)
        : this(clock, DefaultWindow)
    {
    }

    public bool IsPending
    {
        get
        {
            lock (_gate)
            {
                return _sent is not null && !IsExpired();
            }
        }
    }

    public void RecordSent(IEnumerable<ElementRef> elements)
    {
        lock (_gate)
        {
            _sent = [.. elements];
            _sentAt = clock.Now;
        }
    }

    public bool ShouldDrop(IEnumerable<ElementRef> incoming)
    {
        lock (_gate)
        {
            if (_sent is null)
            {
                return false;
            }

            if (IsExpired())
            {
                _sent = null;
                return false;
            }

            if (!_sent.SetEquals(incoming))
            {
                return false;
            }

            _sent = null;
            return true;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _sent = null;
        }
    }

    private bool IsExpired()
    {
        return clock.Now - _sentAt > window;
    }
}
