using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Panel;

namespace DevTools.ModelsContext.Visibility;

internal sealed class ReadBackCache(PanelRuntime runtime)
{
    private readonly Dictionary<(DocumentKey Model, string ViewId), HashSet<long>> _touched = [];
    private readonly List<Func<IEnumerable<ElementRef>>> _watched = [];
    private ReadBackState? _state;

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
        _state = null;
    }

    public void Forget(ViewInfo view)
    {
        if (_state?.Key == KeyOf(view))
        {
            _state = null;
        }
    }

    public void ForgetSource(SourceId source)
    {
        if (_state is { } state && runtime.Store.Find(state.Key.Model) is { } info && info.Source == source)
        {
            _state = null;
        }
    }

    public void ForgetModel(DocumentKey document)
    {
        if (_state?.Key.Model.ModelId == document.ModelId)
        {
            _state = null;
        }

        foreach (var key in _touched.Keys.Where(key => key.Model.ModelId == document.ModelId).ToList())
        {
            _touched.Remove(key);
        }
    }

    public HashSet<long>? HiddenOf(ViewInfo? view, DocumentKey document)
    {
        return view is { } current && StateOf(current, document) is { } state ? state.Hidden : null;
    }

    public HashSet<long>? ReadOf(ViewInfo view, DocumentKey document)
    {
        return StateOf(view, document)?.Read;
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

    public ReadBackRequest RequestFor(ViewInfo view)
    {
        var document = view.Model.CurrentModel;
        return new ReadBackRequest(document, [.. CandidatesOf(KeyOf(view), document)]);
    }

    public void Store(ViewInfo view, ReadBackResult? result)
    {
        if (result is null || result.Document != view.Model.CurrentModel)
        {
            _state = null;
            return;
        }

        var state = new ReadBackState(KeyOf(view));
        state.Read.UnionWith(result.Read);
        state.Hidden.UnionWith(result.Hidden);
        _state = state;
    }

    public async Task<bool> ReadBackAsync(ViewInfo view, CancellationToken cancellationToken)
    {
        var document = view.Model.CurrentModel;
        var key = KeyOf(view);
        if (_state is not { } state || state.Key != key)
        {
            state = new ReadBackState(key);
            _state = state;
        }

        var candidates = CandidatesOf(key, document);
        candidates.ExceptWith(state.Read);
        if (candidates.Count == 0)
        {
            return false;
        }

        var result = await runtime.Services.ViewActions.ReadBackAsync(
            new ReadBackRequest(document, [.. candidates]), cancellationToken);
        state.Read.UnionWith(candidates);
        var before = state.Hidden.Count;
        state.Hidden.UnionWith(result.Hidden);
        return state.Hidden.Count != before;
    }

    private ReadBackState? StateOf(ViewInfo view, DocumentKey document)
    {
        return view.Model.CurrentModel == document && _state is { } state && state.Key == KeyOf(view) ? state : null;
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

    private sealed class ReadBackState((DocumentKey Model, string ViewId) key)
    {
        public (DocumentKey Model, string ViewId) Key { get; } = key;

        public HashSet<long> Read { get; } = [];

        public HashSet<long> Hidden { get; } = [];
    }
}
