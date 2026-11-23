using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Panel;

namespace DevTools.ModelsContext.Selection;

public sealed class SelectionGuard(IClock clock, TimeSpan window)
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private readonly List<(HashSet<ElementRef> Elements, DateTimeOffset SentAt)> _sent = [];

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
                Prune();
                return _sent.Count > 0;
            }
        }
    }

    public void RecordSent(IEnumerable<ElementRef> elements)
    {
        lock (_gate)
        {
            Prune();
            _sent.Add(([.. elements], clock.Now));
        }
    }

    public bool ShouldDrop(IEnumerable<ElementRef> incoming)
    {
        lock (_gate)
        {
            Prune();
            var set = incoming.ToList();
            var index = _sent.FindLastIndex(sent => sent.Elements.SetEquals(set));
            if (index < 0)
            {
                return false;
            }

            _sent.RemoveRange(0, index + 1);
            return true;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _sent.Clear();
        }
    }

    private void Prune()
    {
        var now = clock.Now;
        _sent.RemoveAll(sent => now - sent.SentAt > window);
    }
}
