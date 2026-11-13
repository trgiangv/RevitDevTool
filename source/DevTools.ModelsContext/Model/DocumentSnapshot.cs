namespace DevTools.ModelsContext.Model;

public sealed class DocumentSnapshot
{
    private readonly Dictionary<long, int> _groupIndex = [];
    private readonly List<GroupRow> _groups = [];
    private readonly Dictionary<long, int> _rowIndex = [];
    private readonly List<ElementRow> _rows = [];
    private readonly Dictionary<long, int> _typeIndex = [];
    private readonly List<TypeRow> _types = [];

    internal DocumentSnapshot(SourceId source, int groupingDepth)
    {
        if (groupingDepth < 1 || groupingDepth > TreeSchema.MaxGroupingDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(groupingDepth));
        }

        Source = source;
        GroupingDepth = groupingDepth;
    }

    public SourceId Source { get; }

    public int GroupingDepth { get; }

    public NameTable Names { get; } = new();

    public IReadOnlyList<GroupRow> Groups => _groups;

    public IReadOnlyList<TypeRow> Types => _types;

    public IReadOnlyList<ElementRow> Rows => _rows;

    public CountNode Counts { get; } = new(-1);

    public int ElementCount => _rows.Count;

    public bool IsDirty { get; private set; }

    public int Version { get; private set; }

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

    public IEnumerable<long> IdsUnder(RowPath branch)
    {
        if (branch.ElementId != RowPath.Unset)
        {
            if (Contains(branch.ElementId))
            {
                yield return branch.ElementId;
            }

            yield break;
        }

        foreach (var row in _rows)
        {
            if (IsUnder(row, branch))
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

    internal int UpsertType(TypeRecord type, out bool moved)
    {
        moved = false;
        var groupIndex = UpsertGroup(type.GroupId, type.GroupName);
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
        if (old.GroupIndex != row.GroupIndex || old.TypeIndex != row.TypeIndex)
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
        var last = _rows.Count - 1;
        if (index != last)
        {
            var moved = _rows[last];
            _rows[index] = moved;
            _rowIndex[moved.Id] = index;
        }

        _rows.RemoveAt(last);
        _rowIndex.Remove(id);
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

        row = new ElementRow(element.Id, _types[typeIndex].GroupIndex, typeIndex, element.LevelId, element.WorksetId);
        return true;
    }

    private int UpsertGroup(long groupId, string name)
    {
        var nameIndex = Names.Intern(name);
        if (_groupIndex.TryGetValue(groupId, out var index))
        {
            _groups[index] = new GroupRow(groupId, nameIndex);
            return index;
        }

        index = _groups.Count;
        _groups.Add(new GroupRow(groupId, nameIndex));
        _groupIndex.Add(groupId, index);
        return index;
    }

    private void Count(ElementRow row, int amount)
    {
        Span<int> path = stackalloc int[TreeSchema.MaxGroupingDepth];
        var length = PathOf(row, path);
        Counts.Add(path[..length], amount);
    }
}
