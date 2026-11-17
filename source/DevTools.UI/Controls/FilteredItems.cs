using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace DevTools.UI.Controls;

public sealed class FilteredItems : ObservableCollection<object>
{
    public const int MaxEdits = 64;

    private static readonly PropertyChangedEventArgs CountChanged = new(nameof(Count));
    private static readonly PropertyChangedEventArgs IndexerChanged = new("Item[]");

    private readonly IList _source;
    private readonly List<object> _first = [];
    private bool _isReordered;
    private readonly List<object> _wanted = [];

    public FilteredItems(IList source)
    {
        _source = source;
        foreach (var item in source)
        {
            Items.Add(item);
        }
    }

    public void Apply(Predicate<object>? filter, Predicate<object>? first = null)
    {
        _wanted.Clear();
        _first.Clear();
        for (var index = 0; index < _source.Count; index++)
        {
            var item = _source[index];
            if (filter is null || filter(item))
            {
                _wanted.Add(item);
                if (first is not null && first(item))
                {
                    _first.Add(item);
                }
            }
        }

        if (_first.Count > 0 && _first.Count < _wanted.Count)
        {
            var rest = _wanted.Where(item => !_first.Any(head => ReferenceEquals(head, item))).ToList();
            _wanted.Clear();
            _wanted.AddRange(_first);
            _wanted.AddRange(rest);
            if (!Items.SequenceEqual(_wanted))
            {
                ReplaceAll();
            }

            _isReordered = true;
            return;
        }

        if (_isReordered)
        {
            _isReordered = false;
            ReplaceAll();
            return;
        }

        var edits = CountEdits();
        if (edits == 0)
        {
            return;
        }

        if (edits > MaxEdits)
        {
            ReplaceAll();
            return;
        }

        Merge();
    }

    private int CountEdits()
    {
        int shown = 0, wanted = 0, edits = 0;
        for (var index = 0; index < _source.Count; index++)
        {
            var item = _source[index];
            var isShown = IsAt(Items, shown, item);
            var isWanted = IsAt(_wanted, wanted, item);
            shown += isShown ? 1 : 0;
            wanted += isWanted ? 1 : 0;
            edits += isShown == isWanted ? 0 : 1;
        }

        return edits;
    }

    private void Merge()
    {
        int position = 0, wanted = 0;
        for (var index = 0; index < _source.Count; index++)
        {
            var item = _source[index];
            var isShown = IsAt(Items, position, item);
            var isWanted = IsAt(_wanted, wanted, item);
            wanted += isWanted ? 1 : 0;
            if (isShown == isWanted)
            {
                position += isShown ? 1 : 0;
            }
            else if (isShown)
            {
                RemoveAt(position);
            }
            else
            {
                Insert(position++, item);
            }
        }
    }

    private void ReplaceAll()
    {
        CheckReentrancy();
        Items.Clear();
        foreach (var item in _wanted)
        {
            Items.Add(item);
        }

        OnPropertyChanged(CountChanged);
        OnPropertyChanged(IndexerChanged);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private static bool IsAt(IList<object> list, int index, object item)
    {
        return index < list.Count && ReferenceEquals(list[index], item);
    }
}
