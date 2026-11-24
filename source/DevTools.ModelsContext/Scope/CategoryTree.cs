using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Scope;

public static class CategoryTree
{
    public static IReadOnlyList<GroupCount> ChildrenOf(IEnumerable<GroupCount> groups, long parentId)
    {
        return groups.Where(group => group.ParentId == parentId && group.GroupId != parentId).ToList();
    }

    public static IReadOnlyList<long> WithChildren(IReadOnlyCollection<GroupCount> groups, IEnumerable<long> ids)
    {
        var result = new List<long>();
        var seen = new HashSet<long>();
        foreach (var id in ids)
        {
            if (seen.Add(id))
            {
                result.Add(id);
            }

            foreach (var child in ChildrenOf(groups, id))
            {
                if (seen.Add(child.GroupId))
                {
                    result.Add(child.GroupId);
                }
            }
        }

        return result;
    }

    public static IReadOnlyList<GroupCount> WithParents(IReadOnlyList<GroupCount> groups, IEnumerable<DocumentSnapshot> snapshots)
    {
        var ids = groups.Select(group => group.GroupId).ToHashSet();
        var missing = groups.Where(group => group.ParentId != GroupRow.TopLevel && !ids.Contains(group.ParentId))
            .Select(group => group.ParentId).ToHashSet();
        if (missing.Count == 0)
        {
            return groups;
        }

        var added = new List<GroupCount>();
        foreach (var snapshot in snapshots)
        {
            foreach (var group in snapshot.Groups.Where(group => missing.Contains(group.GroupId)))
            {
                missing.Remove(group.GroupId);
                added.Add(new GroupCount(group.GroupId, snapshot.Names[group.NameIndex], 0, group.ParentId));
            }
        }

        return groups.Concat(added).OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
