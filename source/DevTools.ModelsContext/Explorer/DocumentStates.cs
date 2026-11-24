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
            var outside = ScopeCounter.Count(document.Snapshot, hidden.Where(id => !document.InScope(id)));
            var unseen = UnseenOf(data, document, hidden, visibility.View);
            _states[document.Key] = new DocumentState(hidden, ScopeCounter.Count(document.Snapshot, hidden), outside,
                overrides, unseen, ScopeCounter.Count(document.Snapshot, unseen));
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

    private static HashSet<long> UnseenOf(ContextData data, DocumentScope document, HashSet<long> hidden, ViewInfo? view)
    {
        if (data.Scope != ScopeKind.ActiveView)
        {
            return [];
        }

        var unseen = new HashSet<long>(document.OutsideIds(view));
        unseen.ExceptWith(hidden);
        return unseen;
    }
}

internal sealed class DocumentState(
    HashSet<long> hiddenIds,
    CountNode hidden,
    CountNode outside,
    OverrideTally overrides,
    HashSet<long> unseenIds,
    CountNode unseen)
{
    public HashSet<long> HiddenIds { get; } = hiddenIds;

    public CountNode Hidden { get; } = hidden;

    public CountNode Outside { get; } = outside;

    public OverrideTally Overrides { get; } = overrides;

    public HashSet<long> UnseenIds { get; } = unseenIds;

    public CountNode Unseen { get; } = unseen;

    public CountNode Selected { get; set; } = new(-1);
}
