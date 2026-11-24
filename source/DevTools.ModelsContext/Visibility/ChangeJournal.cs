using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Visibility;

public sealed record LinkHide(ElementRef Element, string UniqueId, bool IsIsolateRest = false);

public sealed record JournalPage(int ChangeCount, IReadOnlyList<LinkHide> LinkHides, bool IsStale)
{
    public static JournalPage Empty { get; } = new(0, [], false);
}

public sealed class ChangeJournal
{
    private readonly object _gate = new();
    private readonly Dictionary<(DocumentKey Model, string ViewId), Page> _pages = [];

    public void AddChange(DocumentKey model, string viewId, bool keepConverts)
    {
        lock (_gate)
        {
            var page = PageOf(model.CurrentModel, viewId);
            if (keepConverts)
            {
                page.HideChanges++;
            }
            else
            {
                page.PropertyChanges++;
            }
        }
    }

    public void Keep(DocumentKey model, string viewId)
    {
        lock (_gate)
        {
            if (_pages.TryGetValue((model.CurrentModel, viewId), out var page))
            {
                page.HideChanges = 0;
                page.Hides.Clear();
                DropIfEmpty(model.CurrentModel, viewId, page);
            }
        }
    }

    public void Rename(ModelRename rename)
    {
        lock (_gate)
        {
            foreach (var pair in _pages.Where(pair => pair.Key.Model.ModelId == rename.OldId).ToList())
            {
                _pages.Remove(pair.Key);
                var page = pair.Value;
                var hides = page.Hides.Values.ToList();
                page.Hides.Clear();
                foreach (var batched in hides)
                {
                    var element = rename.Map(batched.Hide.Element);
                    page.Hides[element] = batched with { Hide = batched.Hide with { Element = element } };
                }

                _pages[(rename.Map(pair.Key.Model), pair.Key.ViewId)] = page;
            }
        }
    }

    public void AddLinkHides(DocumentKey model, string viewId, IEnumerable<LinkHide> hides)
    {
        lock (_gate)
        {
            var page = PageOf(model.CurrentModel, viewId);
            var batch = ++page.Batches;
            foreach (var hide in hides)
            {
                if (!hide.IsIsolateRest || !page.Hides.ContainsKey(hide.Element))
                {
                    page.Hides[hide.Element] = new Batched(hide, batch);
                }
            }
        }
    }

    public void EndIsolateRest(DocumentKey model, string viewId)
    {
        lock (_gate)
        {
            if (!_pages.TryGetValue((model.CurrentModel, viewId), out var page))
            {
                return;
            }

            foreach (var pair in page.Hides.Where(pair => pair.Value.Hide.IsIsolateRest).ToList())
            {
                page.Hides[pair.Key] = pair.Value with { Hide = pair.Value.Hide with { IsIsolateRest = false } };
            }
        }
    }

    public bool HasLinkHide(DocumentKey model, string viewId, ElementRef element)
    {
        lock (_gate)
        {
            return _pages.TryGetValue((model.CurrentModel, viewId), out var page) && page.Hides.ContainsKey(element);
        }
    }

    public HashSet<long> LinkHideIds(DocumentKey model, string viewId, DocumentKey document)
    {
        lock (_gate)
        {
            return _pages.TryGetValue((model.CurrentModel, viewId), out var page)
                ? [.. page.Hides.Keys.Where(element => element.Document == document).Select(element => element.Id)]
                : [];
        }
    }

    public JournalPage Get(DocumentKey model, string viewId)
    {
        lock (_gate)
        {
            if (!_pages.TryGetValue((model.CurrentModel, viewId), out var page))
            {
                return JournalPage.Empty;
            }

            var changes = page.HideChanges + page.PropertyChanges + page.Hides.Values.Select(batched => batched.Batch).Distinct().Count();
            return new JournalPage(changes, page.Hides.Values.Select(batched => batched.Hide).ToList(), page.IsStale);
        }
    }

    public void ClearChanges(DocumentKey model, string viewId)
    {
        lock (_gate)
        {
            if (_pages.TryGetValue((model.CurrentModel, viewId), out var page))
            {
                page.HideChanges = 0;
                page.PropertyChanges = 0;
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
                page.Hides.Remove(element);
            }

            DropIfEmpty(model.CurrentModel, viewId, page);
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
                foreach (var element in page.Hides.Keys.Where(element => element.Document == link).ToList())
                {
                    page.Hides.Remove(element);
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
        if (page.HideChanges == 0 && page.PropertyChanges == 0 && page.Hides.Count == 0)
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

    private readonly record struct Batched(LinkHide Hide, int Batch);

    private sealed class Page
    {
        public int HideChanges { get; set; }

        public int PropertyChanges { get; set; }

        public int Batches { get; set; }

        public Dictionary<ElementRef, Batched> Hides { get; } = [];

        public bool IsStale { get; set; }
    }
}
