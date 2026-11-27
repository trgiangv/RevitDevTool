using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Visibility;
using System.Diagnostics.CodeAnalysis;

namespace DevTools.ModelsContext.Explorer;

internal sealed class DocumentStates(VisibilityController visibility)
{
    private readonly Dictionary<DocumentKey, DocumentState> _states = [];

    public IEnumerable<DocumentState> All => _states.Values;

    public DocumentState this[DocumentKey key] => _states[key];

    public bool TryGet(DocumentKey key, [MaybeNullWhen(false)] out DocumentState state)
    {
        return _states.TryGetValue(key, out state);
    }

    public void Clear()
    {
        _states.Clear();
    }

    public void Compute(ContextData data, IReadOnlyCollection<ElementRef> selected)
    {
        _states.Clear();
        foreach (var document in data.Documents)
        {
            var hidden = visibility.HiddenIds(document);
            var overrides = OverrideTally.Count(document.Snapshot,
                visibility.OverridesOf(document.Key).Where(pair => document.InScope(pair.Key)));
            var hiddenElsewhere = ScopeCounter.Count(document.Snapshot, hidden.Where(id => !document.InScope(id)));
            var dimmed = DimmedOf(data, document, hidden, visibility.View);
            var notDrawn = ScopeCounter.Count(document.Snapshot, visibility.NotDrawnIds(document.Key)
                .Where(id => document.InScope(id) && !hidden.Contains(id)));
            _states[document.Key] = new DocumentState(hidden, ScopeCounter.Count(document.Snapshot, hidden), hiddenElsewhere,
                overrides, dimmed, ScopeCounter.Count(document.Snapshot, dimmed))
            {
                NotDrawn = notDrawn
            };
        }

        ComputeSelected(data, selected);
    }

    public void ComputeSelected(ContextData data, IReadOnlyCollection<ElementRef> selected)
    {
        foreach (var document in data.Documents)
        {
            if (_states.TryGetValue(document.Key, out var state))
            {
                state.Selected = ScopeCounter.Count(document.Snapshot, selected
                    .Where(element => element.Document == document.Key)
                    .Select(element => element.Id));
            }
        }
    }

    private static HashSet<long> DimmedOf(ContextData data, DocumentScope document, HashSet<long> hidden, ViewInfo? view)
    {
        if (data.Scope != ScopeKind.ActiveView)
        {
            return [];
        }

        var dimmed = new HashSet<long>(document.UndrawnIds(view));
        dimmed.ExceptWith(hidden);
        return dimmed;
    }
}

internal sealed class DocumentState(
    HashSet<long> hiddenIds,
    CountNode hidden,
    CountNode hiddenElsewhere,
    OverrideTally overrides,
    HashSet<long> dimmedIds,
    CountNode dimmed)
{
    public HashSet<long> HiddenIds { get; } = hiddenIds;

    public CountNode Hidden { get; } = hidden;

    public CountNode HiddenElsewhere { get; } = hiddenElsewhere;

    public OverrideTally Overrides { get; } = overrides;

    public HashSet<long> DimmedIds { get; } = dimmedIds;

    public CountNode Dimmed { get; } = dimmed;

    public CountNode NotDrawn { get; init; } = new(-1);

    public CountNode Selected { get; set; } = new(-1);
}
