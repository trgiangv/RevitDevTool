using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Scope;

namespace DevTools.ModelsContext.Documents;

public sealed class DocumentSnapshot
{
    private readonly Dictionary<long, int> _groupIndex = [];
    private readonly List<GroupRow> _groups = [];
    private readonly RowIdIndex _rowIndex;
    private readonly List<ElementRow> _rows = [];
    private readonly Dictionary<long, int> _typeIndex = [];
    private readonly List<TypeRow> _types = [];

    internal DocumentSnapshot(SourceId source, int groupingDepth)
    {
        if (groupingDepth is < 1 or > TreeSchema.MaxGroupingDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(groupingDepth));
        }

        Source = source;
        GroupingDepth = groupingDepth;
        _rowIndex = new RowIdIndex(_rows);
    }

    public SourceId Source { get; private set; }

    internal void Rename(SourceId source)
    {
        Source = source;
    }

    public int GroupingDepth { get; }

    public NameTable Names { get; } = new();

    public IReadOnlyList<GroupRow> Groups => _groups;

    public IReadOnlyList<TypeRow> Types => _types;

    public IReadOnlyList<ElementRow> Rows => _rows;

    public CountNode Counts { get; } = new(-1);

    public int ElementCount => _rows.Count;

    public bool IsDirty { get; private set; }

    public int Version { get; private set; }

    public DocumentVersion? Stamp { get; private set; }

    public long EstimatedBytes
    {
        get
        {
            const int elementRowBytes = 48;
            const int typeRowBytes = 24;
            const int groupRowBytes = 16;
            const int indexEntryBytes = 28;
            const int slotBytes = 4;
            const int nameOverheadBytes = 52;
            const int countNodeBytes = 96;
            var bytes = (long)_rows.Capacity * elementRowBytes
                        + (long)_rowIndex.SlotCount * slotBytes
                        + (long)_types.Capacity * typeRowBytes
                        + (long)_typeIndex.Count * indexEntryBytes
                        + (long)_groups.Capacity * groupRowBytes
                        + (long)_groupIndex.Count * indexEntryBytes
                        + (long)CountNodes(Counts) * countNodeBytes;
            foreach (var name in Names.Names)
            {
                bytes += nameOverheadBytes + 2L * name.Length;
            }

            return bytes;
        }
    }

    public bool TryGetRow(long id, out ElementRow row)
    {
        if (_rowIndex.TryGetValue(id, out var index))
        {
            row = _rows[index];
            return true;
        }

        row = default;
        return false;
    }

    public bool Contains(long id)
    {
        return _rowIndex.ContainsKey(id);
    }

    public bool InModel(long id)
    {
        return TryGetRow(id, out var row) && !row.SecondaryOption;
    }

    public int GroupIndexOf(long groupId)
    {
        return _groupIndex.TryGetValue(groupId, out var index) ? index : -1;
    }

    public int TypeIndexOf(long typeId)
    {
        return _typeIndex.TryGetValue(typeId, out var index) ? index : -1;
    }

    public string GroupName(int groupIndex)
    {
        return Names[_groups[groupIndex].NameIndex];
    }

    public string FamilyName(int typeIndex)
    {
        return Names[_types[typeIndex].FamilyNameIndex];
    }

    public string TypeName(int typeIndex)
    {
        return Names[_types[typeIndex].TypeNameIndex];
    }

    public int PathOf(ElementRow row, Span<int> path)
    {
        path[0] = row.GroupIndex;
        switch (GroupingDepth)
        {
            case 1:
                return 1;
            case 2:
                path[1] = row.TypeIndex;
                return 2;
            default:
                path[1] = _types[row.TypeIndex].FamilyNameIndex;
                path[2] = row.TypeIndex;
                return 3;
        }
    }

    public bool IsUnder(ElementRow row, RowPath branch)
    {
        if (branch.ElementId != RowPath.Unset)
        {
            return row.Id == branch.ElementId;
        }

        if (branch.Group != RowPath.Any && row.GroupIndex != branch.Group)
        {
            return false;
        }

        if (branch.Type != RowPath.Any && row.TypeIndex != branch.Type)
        {
            return false;
        }

        return branch.Family == RowPath.Any || _types[row.TypeIndex].FamilyNameIndex == branch.Family;
    }

    public IReadOnlyList<RowPath> WithSubgroups(RowPath branch)
    {
        if (branch.ElementId != RowPath.Unset || branch.Family != RowPath.Any || branch.Type != RowPath.Any
            || branch.Group < 0 || branch.Group >= _groups.Count)
        {
            return [branch];
        }

        var parentId = _groups[branch.Group].GroupId;
        var paths = new List<RowPath> { branch };
        for (var index = 0; index < _groups.Count; index++)
        {
            if (_groups[index].ParentId == parentId && index != branch.Group)
            {
                paths.Add(RowPath.ForGroup(index));
            }
        }

        return paths;
    }

    public IEnumerable<long> IdsUnder(RowPath branch)
    {
        return IdsUnder(branch, false);
    }

    public IEnumerable<long> ModelIdsUnder(RowPath branch)
    {
        return IdsUnder(branch, true);
    }

    private IEnumerable<long> IdsUnder(RowPath branch, bool modelOnly)
    {
        if (branch.ElementId != RowPath.Unset)
        {
            if (TryGetRow(branch.ElementId, out var element) && !(modelOnly && element.SecondaryOption))
            {
                yield return branch.ElementId;
            }

            yield break;
        }

        foreach (var row in _rows)
        {
            if (!(modelOnly && row.SecondaryOption) && IsUnder(row, branch))
            {
                yield return row.Id;
            }
        }
    }

    internal void MarkDirty()
    {
        IsDirty = true;
        Version++;
    }

    internal void SetStamp(DocumentVersion? stamp)
    {
        Stamp = stamp;
    }

    internal void TrimExcess()
    {
        _rows.TrimExcess();
        _rowIndex.TrimExcess();
        _types.TrimExcess();
        _groups.TrimExcess();
    }

    private static int CountNodes(CountNode node)
    {
        var count = 1;
        foreach (var child in node.Children.Values)
        {
            count += CountNodes(child);
        }

        return count;
    }

    internal int UpsertType(TypeRecord type, out bool moved)
    {
        moved = false;
        var groupIndex = UpsertGroup(type.GroupId, type.GroupName, type.ParentId);
        var familyIndex = Names.Intern(type.FamilyName);
        var typeNameIndex = Names.Intern(type.TypeName);
        var row = new TypeRow(type.TypeId, groupIndex, familyIndex, typeNameIndex);
        if (_typeIndex.TryGetValue(type.TypeId, out var index))
        {
            var old = _types[index];
            moved = old.GroupIndex != groupIndex || (old.FamilyNameIndex != familyIndex && GroupingDepth == 3);
            _types[index] = row;
            Version++;
            return index;
        }

        index = _types.Count;
        _types.Add(row);
        _typeIndex.Add(type.TypeId, index);
        return index;
    }

    internal bool TryAdd(ElementRecord element)
    {
        if (_rowIndex.ContainsKey(element.Id) || !TryCreateRow(element, out var row))
        {
            return false;
        }

        _rowIndex.Add(row.Id, _rows.Count);
        _rows.Add(row);
        Count(row, 1);
        Version++;
        return true;
    }

    internal bool TryReplace(ElementRecord element)
    {
        if (!_rowIndex.TryGetValue(element.Id, out var index) || !TryCreateRow(element, out var row))
        {
            return false;
        }

        var old = _rows[index];
        if (old.GroupIndex != row.GroupIndex || old.TypeIndex != row.TypeIndex
                                             || old.SecondaryOption != row.SecondaryOption)
        {
            Count(old, -1);
            Count(row, 1);
        }

        _rows[index] = row;
        Version++;
        return true;
    }

    internal bool Remove(long id)
    {
        if (!_rowIndex.TryGetValue(id, out var index))
        {
            return false;
        }

        Count(_rows[index], -1);
        _rowIndex.Remove(id);
        var last = _rows.Count - 1;
        if (index != last)
        {
            var moved = _rows[last];
            _rowIndex.Move(moved.Id, index);
            _rows[index] = moved;
        }

        _rows.RemoveAt(last);
        Version++;
        return true;
    }

    private bool TryCreateRow(ElementRecord element, out ElementRow row)
    {
        var typeIndex = TypeIndexOf(element.TypeId);
        if (typeIndex < 0)
        {
            row = default;
            return false;
        }

        row = new ElementRow(element.Id, _types[typeIndex].GroupIndex, typeIndex, element.LevelId, element.WorksetId,
            element.SecondaryOption, element.Draw, element.OwnerViewId);
        return true;
    }

    private int UpsertGroup(long groupId, string name, long parentId)
    {
        var group = new GroupRow(groupId, Names.Intern(name), parentId);
        if (_groupIndex.TryGetValue(groupId, out var index))
        {
            _groups[index] = group;
            return index;
        }

        index = _groups.Count;
        _groups.Add(group);
        _groupIndex.Add(groupId, index);
        return index;
    }

    private void Count(ElementRow row, int amount)
    {
        if (row.SecondaryOption)
        {
            return;
        }

        Span<int> path = stackalloc int[TreeSchema.MaxGroupingDepth];
        var length = PathOf(row, path);
        Counts.Add(path[..length], amount);
    }
}
