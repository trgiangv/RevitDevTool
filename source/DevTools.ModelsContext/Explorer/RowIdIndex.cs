using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Explorer;

internal sealed class RowIdIndex(List<ElementRow> rows)
{
    private const int MinimumSlots = 16;

    private int _count;
    private int _mask = MinimumSlots - 1;
    private int[] _slots = new int[MinimumSlots];

    public int SlotCount => _slots.Length;

    public bool TryGetValue(long id, out int row)
    {
        var slot = Find(id);
        row = slot < 0 ? -1 : _slots[slot] - 1;
        return slot >= 0;
    }

    public bool ContainsKey(long id)
    {
        return Find(id) >= 0;
    }

    public void Add(long id, int row)
    {
        if ((_count + 1) * 4L > _slots.Length * 3L)
        {
            Resize(_slots.Length * 2);
        }

        var slot = SlotOf(id);
        while (_slots[slot] != 0)
        {
            slot = (slot + 1) & _mask;
        }

        _slots[slot] = row + 1;
        _count++;
    }

    public void Move(long id, int row)
    {
        _slots[Find(id)] = row + 1;
    }

    public void Remove(long id)
    {
        var hole = Find(id);
        if (hole < 0)
        {
            return;
        }

        _slots[hole] = 0;
        _count--;
        var slot = (hole + 1) & _mask;
        while (_slots[slot] != 0)
        {
            var home = SlotOf(rows[_slots[slot] - 1].Id);
            if (((slot - home) & _mask) >= ((slot - hole) & _mask))
            {
                _slots[hole] = _slots[slot];
                _slots[slot] = 0;
                hole = slot;
            }

            slot = (slot + 1) & _mask;
        }
    }

    public void TrimExcess()
    {
        var size = MinimumSlots;
        while (_count * 4L > size * 3L)
        {
            size *= 2;
        }

        if (size < _slots.Length)
        {
            Resize(size);
        }
    }

    private int Find(long id)
    {
        var slot = SlotOf(id);
        while (_slots[slot] is var value and not 0)
        {
            if (rows[value - 1].Id == id)
            {
                return slot;
            }

            slot = (slot + 1) & _mask;
        }

        return -1;
    }

    private int SlotOf(long id)
    {
        var hash = (ulong)id * 0x9E3779B97F4A7C15UL;
        return (int)(hash >> 32) & _mask;
    }

    private void Resize(int size)
    {
        var old = _slots;
        _slots = new int[size];
        _mask = size - 1;
        foreach (var value in old)
        {
            if (value == 0)
            {
                continue;
            }

            var slot = SlotOf(rows[value - 1].Id);
            while (_slots[slot] != 0)
            {
                slot = (slot + 1) & _mask;
            }

            _slots[slot] = value;
        }
    }
}
