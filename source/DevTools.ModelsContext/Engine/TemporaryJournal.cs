using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Engine;

public sealed record JournalEntry(ElementRef Element, string UniqueId, VisibilityAction Action);

public sealed record JournalState(int ChangeCount, IReadOnlyList<JournalEntry> LinkEntries, bool IsStale)
{
    public static JournalState Empty { get; } = new(0, [], false);
}

public sealed class TemporaryJournal
{
    private readonly object _gate = new();
    private readonly Dictionary<(DocumentKey Model, string ViewId), Page> _pages = [];

    public void Record(DocumentKey model, string viewId, IEnumerable<JournalEntry>? linkEntries = null)
    {
        lock (_gate)
        {
            var page = PageOf(model.CurrentModel, viewId);
            page.ChangeCount++;
            if (linkEntries is null)
            {
                return;
            }

            foreach (var entry in linkEntries)
            {
                page.Entries[entry.Element] = entry;
            }
        }
    }

    public JournalState Get(DocumentKey model, string viewId)
    {
        lock (_gate)
        {
            return _pages.TryGetValue((model.CurrentModel, viewId), out var page)
                ? new JournalState(page.ChangeCount, page.Entries.Values.ToList(), page.IsStale)
                : JournalState.Empty;
        }
    }

    public VisibilityAction? ActionOf(DocumentKey model, string viewId, ElementRef element)
    {
        lock (_gate)
        {
            if (!_pages.TryGetValue((model.CurrentModel, viewId), out var page) || page.IsStale)
            {
                return null;
            }

            return page.Entries.TryGetValue(element, out var entry) ? entry.Action : null;
        }
    }

    public void Reset(DocumentKey model, string viewId)
    {
        lock (_gate)
        {
            _pages.Remove((model.CurrentModel, viewId));
        }
    }

    public void ClearModel(DocumentKey model)
    {
        lock (_gate)
        {
            foreach (var key in _pages.Keys.Where(key => key.Model.ModelId == model.ModelId).ToList())
            {
                _pages.Remove(key);
            }
        }
    }

    public void ClearLink(DocumentKey link)
    {
        lock (_gate)
        {
            foreach (var page in _pages.Where(pair => pair.Key.Model.ModelId == link.ModelId).Select(pair => pair.Value))
            {
                foreach (var element in page.Entries.Keys.Where(element => element.Document == link).ToList())
                {
                    page.Entries.Remove(element);
                }
            }
        }
    }

    public void MarkStale(DocumentKey model)
    {
        lock (_gate)
        {
            foreach (var pair in _pages.Where(pair => pair.Key.Model.ModelId == model.ModelId))
            {
                pair.Value.IsStale = true;
            }
        }
    }

    private Page PageOf(DocumentKey model, string viewId)
    {
        if (!_pages.TryGetValue((model, viewId), out var page))
        {
            page = new Page();
            _pages.Add((model, viewId), page);
        }

        return page;
    }

    private sealed class Page
    {
        public int ChangeCount { get; set; }

        public Dictionary<ElementRef, JournalEntry> Entries { get; } = [];

        public bool IsStale { get; set; }
    }
}
