using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Panel;

namespace DevTools.ModelsContext.Visibility;

internal sealed class ReadBackCache(PanelRuntime runtime)
{
    private readonly Dictionary<(DocumentKey Model, string ViewId), ReadBack> _readBacks = [];
    private readonly Dictionary<(DocumentKey Model, string ViewId), HashSet<long>> _touched = [];
    private readonly List<Func<IEnumerable<ElementRef>>> _watched = [];

    public static (DocumentKey Model, string ViewId) KeyOf(ViewInfo view)
    {
        return (view.Model.CurrentModel, view.ViewId);
    }

    public void Watch(Func<IEnumerable<ElementRef>> elements)
    {
        _watched.Add(elements);
    }

    public void Clear()
    {
        _readBacks.Clear();
    }

    public void Drop(ViewInfo view)
    {
        _readBacks.Remove(KeyOf(view));
    }

    public void DropSource(SourceId source)
    {
        foreach (var key in _readBacks.Keys
                     .Where(key => runtime.Store.Find(key.Model) is { } info && info.Source == source)
                     .ToList())
        {
            _readBacks.Remove(key);
        }
    }

    public void ForgetModel(DocumentKey document)
    {
        foreach (var key in _readBacks.Keys.Where(key => key.Model.ModelId == document.ModelId).ToList())
        {
            _readBacks.Remove(key);
        }

        foreach (var key in _touched.Keys.Where(key => key.Model.ModelId == document.ModelId).ToList())
        {
            _touched.Remove(key);
        }
    }

    public HashSet<long>? HiddenOf(ViewInfo? view, DocumentKey document)
    {
        return view is { } current && current.Model.CurrentModel == document
                                   && _readBacks.TryGetValue(KeyOf(current), out var readBack)
            ? readBack.Hidden
            : null;
    }

    public HashSet<long>? CheckedOf(ViewInfo view, DocumentKey document)
    {
        return view.Model.CurrentModel == document && _readBacks.TryGetValue(KeyOf(view), out var readBack)
            ? readBack.Checked
            : null;
    }

    public void Touch(ViewInfo view, VisibilityPlan plan)
    {
        var model = view.Model.CurrentModel;
        foreach (var step in plan.Steps.OfType<ElementsStep>().Where(step => step.Document == model))
        {
            if (!_touched.TryGetValue(KeyOf(view), out var touched))
            {
                touched = [];
                _touched.Add(KeyOf(view), touched);
            }

            touched.UnionWith(step.Ids);
        }
    }

    public HiddenProbe ProbeOf(ViewInfo view)
    {
        var document = view.Model.CurrentModel;
        return new HiddenProbe(document, [.. CandidatesOf(KeyOf(view), document)]);
    }

    public void Seed(ViewInfo view, HiddenRead read)
    {
        if (read.Document != view.Model.CurrentModel)
        {
            return;
        }

        var readBack = new ReadBack();
        readBack.Checked.UnionWith(read.Checked);
        readBack.Hidden.UnionWith(read.Hidden);
        _readBacks[KeyOf(view)] = readBack;
    }

    public async Task<bool> ReadAsync(ViewInfo view, CancellationToken cancellationToken)
    {
        var document = view.Model.CurrentModel;
        var key = KeyOf(view);
        if (!_readBacks.TryGetValue(key, out var readBack))
        {
            readBack = new ReadBack();
            _readBacks.Add(key, readBack);
        }

        var candidates = CandidatesOf(key, document);
        candidates.ExceptWith(readBack.Checked);
        if (candidates.Count == 0)
        {
            return false;
        }

        var hidden = await runtime.Services.ViewActions.ReadHiddenAsync(document, candidates, cancellationToken);
        readBack.Checked.UnionWith(candidates);
        var before = readBack.Hidden.Count;
        readBack.Hidden.UnionWith(hidden);
        return readBack.Hidden.Count != before;
    }

    private HashSet<long> CandidatesOf((DocumentKey Model, string ViewId) key, DocumentKey document)
    {
        var candidates = new HashSet<long>(_watched.SelectMany(watched => watched())
            .Where(element => element.Document == document)
            .Select(element => element.Id));
        if (_touched.TryGetValue(key, out var touched))
        {
            candidates.UnionWith(touched);
        }

        return candidates;
    }

    private sealed class ReadBack
    {
        public HashSet<long> Checked { get; } = [];

        public HashSet<long> Hidden { get; } = [];
    }
}
