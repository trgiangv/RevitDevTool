using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Scope;

namespace DevTools.ModelsContext.Explorer;

internal static class VisibleIds
{
    public static IEnumerable<ElementRef> Of(ContextData data, List<TreeRow> visible)
    {
        return data.Documents.Where(document => document.Key.IsCurrentModel)
            .SelectMany(document => IdsOf(document, visible)
                .Select(id => new ElementRef(document.Key, id)));
    }

    private static List<long> IdsOf(DocumentScope scope, List<TreeRow> visible)
    {
        var groups = new HashSet<int>();
        var ids = new List<long>();
        foreach (var row in visible)
        {
            Collect(row, scope, groups, ids);
        }

        if (groups.Count > 0)
        {
            ids.AddRange(IdsInGroups(scope, groups));
        }

        return ids;
    }

    private static void Collect(TreeRow row, DocumentScope scope, HashSet<int> groups, List<long> ids)
    {
        switch (row.Kind)
        {
            case TreeRowKind.Group or TreeRowKind.Family or TreeRowKind.Type when row.Document == scope.Key:
                groups.Add(row.Path.Group);
                foreach (var subgroup in row.Subgroups ?? [])
                {
                    groups.Add(subgroup.Path.Group);
                }

                break;
            case TreeRowKind.Instance when row.Document == scope.Key:
                ids.Add(row.Id);
                break;
        }
    }

    private static IEnumerable<long> IdsInGroups(DocumentScope scope, HashSet<int> groups)
    {
        var snapshot = scope.Snapshot;
        if (scope.Ids is null)
        {
            return snapshot.Rows.Where(row => !row.SecondaryOption && groups.Contains(row.GroupIndex))
                .Select(row => row.Id);
        }

        return scope.Ids.Where(id => snapshot.TryGetRow(id, out var row) && groups.Contains(row.GroupIndex));
    }
}
