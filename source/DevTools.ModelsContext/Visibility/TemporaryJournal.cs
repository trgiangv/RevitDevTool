using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Visibility;

public sealed record JournalEntry(ElementRef Element, string UniqueId);

public sealed record JournalState(int ChangeCount, IReadOnlyList<JournalEntry> LinkEntries, bool IsStale)
{
    public static JournalState Empty { get; } = new(0, [], false);
}

public sealed class TemporaryJournal
{
    private readonly object _gate = new();
    private readonly Dictionary<(DocumentKey Model, string ViewId), Page> _pages = [];

    public void Record(DocumentKey model, string viewId)
    {
        lock (_gate)
        {
            PageOf(model.CurrentModel, viewId).TemporaryChanges++;
        }
    }

    public void RecordHidden(DocumentKey model, string viewId, IEnumerable<JournalEntry> entries)
    {
        lock (_gate)
        {
            var page = PageOf(model.CurrentModel, viewId);
            var batch = ++page.Batches;
            foreach (var entry in entries)
            {
                page.Entries[entry.Element] = new Hidden(entry, batch);
            }
        }
    }

    public JournalState Get(DocumentKey model, string viewId)
    {
        lock (_gate)
        {
            if (!_pages.TryGetValue((model.CurrentModel, viewId), out var page))
            {
                return JournalState.Empty;
            }

            var changes = page.TemporaryChanges + page.Entries.Values.Select(hidden => hidden.Batch).Distinct().Count();
            return new JournalState(changes, page.Entries.Values.Select(hidden => hidden.Entry).ToList(), page.IsStale);
        }
    }

    public void ResetTemporary(DocumentKey model, string viewId)
    {
        lock (_gate)
        {
            if (_pages.TryGetValue((model.CurrentModel, viewId), out var page))
            {
                page.TemporaryChanges = 0;
                DropIfEmpty(model.CurrentModel, viewId, page);
            }
        }
    }

    public void Remove(DocumentKey model, string viewId, IEnumerable<ElementRef> elements)
    {
        lock (_gate)
        {
            if (!_pages.TryGetValue((model.CurrentModel, viewId), out var page))
            {
                return;
            }

            foreach (var element in elements)
            {
                page.Entries.Remove(element);
            }

            DropIfEmpty(model.CurrentModel, viewId, page);
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

    private void DropIfEmpty(DocumentKey model, string viewId, Page page)
    {
        if (page.TemporaryChanges == 0 && page.Entries.Count == 0)
        {
            _pages.Remove((model, viewId));
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

    private readonly record struct Hidden(JournalEntry Entry, int Batch);

    private sealed class Page
    {
        public int TemporaryChanges { get; set; }

        public int Batches { get; set; }

        public Dictionary<ElementRef, Hidden> Entries { get; } = [];

        public bool IsStale { get; set; }
    }
}
