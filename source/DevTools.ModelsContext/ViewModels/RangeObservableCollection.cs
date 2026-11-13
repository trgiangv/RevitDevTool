using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace DevTools.ModelsContext.ViewModels;

public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    public const int ItemEventLimit = 512;

    private static readonly PropertyChangedEventArgs CountChanged = new(nameof(Count));
    private static readonly PropertyChangedEventArgs IndexerChanged = new("Item[]");
    private static readonly NotifyCollectionChangedEventArgs ResetArgs = new(NotifyCollectionChangedAction.Reset);

    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        RaiseReset();
    }

    public void InsertRange(int index, IEnumerable<T> items)
    {
        ReplaceRange(index, 0, items);
    }

    public void RemoveRange(int index, int count)
    {
        ReplaceRange(index, count, []);
    }

    public void ReplaceRange(int index, int removeCount, IEnumerable<T> items)
    {
        if (index < 0 || index > Count || removeCount < 0 || index + removeCount > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        CheckReentrancy();
        var inserted = items as IReadOnlyList<T> ?? items.ToList();
        if (removeCount == 0 && inserted.Count == 0)
        {
            return;
        }

        if (removeCount + inserted.Count <= ItemEventLimit)
        {
            for (var position = index + removeCount - 1; position >= index; position--)
            {
                RemoveAt(position);
            }

            for (var offset = 0; offset < inserted.Count; offset++)
            {
                Insert(index + offset, inserted[offset]);
            }

            return;
        }

        var list = (List<T>)Items;
        list.RemoveRange(index, removeCount);
        list.InsertRange(index, inserted);
        RaiseReset();
    }

    private void RaiseReset()
    {
        OnPropertyChanged(CountChanged);
        OnPropertyChanged(IndexerChanged);
        OnCollectionChanged(ResetArgs);
    }
}
