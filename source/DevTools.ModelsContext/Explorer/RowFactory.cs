using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Visibility;
using System.Globalization;

namespace DevTools.ModelsContext.Explorer;

internal static class RowFactory
{
    public static string RowKey(
        TreeRowKind kind,
        DocumentKey? document,
        long group = RowPath.Unset,
        string family = "",
        long type = RowPath.Unset,
        long element = RowPath.Unset)
    {
        return string.Join("|", (int)kind, document?.ToString() ?? string.Empty,
            group.ToString(CultureInfo.InvariantCulture), family, type.ToString(CultureInfo.InvariantCulture),
            element.ToString(CultureInfo.InvariantCulture));
    }


    public static TreeRow RootRow(DocumentKey key, string label)
    {
        return key.IsCurrentModel
            ? new TreeRow(TreeRowKind.CurrentModelHeader, ContextBarViewModel.CurrentModelLabel, document: key)
            {
                HasChildren = true,
                Key = RowKey(TreeRowKind.CurrentModelHeader, key)
            }
            : new TreeRow(TreeRowKind.LinkModel, label, document: key, id: key.LinkInstanceId)
            {
                HasChildren = true,
                Key = RowKey(TreeRowKind.LinkModel, key)
            };
    }

    public static List<TreeRow> QuickGroupRows(TreeRow parent, DocumentKey document, IEnumerable<GroupCount> groups)
    {
        return Sorted(groups.Where(group => group.Count > 0).Select(group =>
            new TreeRow(TreeRowKind.Group, group.Name, parent, id: group.GroupId)
            {
                Count = group.Count,
                DisabledReason = VisibilityText.Updating,
                Key = RowKey(TreeRowKind.Group, document, group.GroupId)
            }));
    }

    public static TreeRow Category(DocumentKey current, long groupId, string name)
    {
        return new TreeRow(TreeRowKind.Category, name, document: current, id: groupId)
        {
            Key = RowKey(TreeRowKind.Category, current, groupId)
        };
    }

    public static TreeRow Instance(TreeRow parent, DocumentKey key, InstanceName name)
    {
        return new TreeRow(TreeRowKind.Instance, name.Name, parent, path: RowPath.ForElement(name.Id), id: name.Id)
        {
            Key = RowKey(TreeRowKind.Instance, key, element: name.Id)
        };
    }

    public static TreeRow MorePage(TreeRow parent, DocumentKey key, int shown, int total)
    {
        return new TreeRow(TreeRowKind.MorePage,
            string.Format(CultureInfo.CurrentCulture, "{0:N0} of {1:N0} · more", shown, total), parent)
        {
            Key = parent.Key + "|" + RowKey(TreeRowKind.MorePage, key)
        };
    }

    public static List<TreeRow> Sorted(IEnumerable<TreeRow> rows)
    {
        return rows.OrderBy(row => row.Text, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static int[] KeyPath(RowPath path, int depth)
    {
        if (path.Group == RowPath.Any)
        {
            return [];
        }

        return depth switch
        {
            1 => [path.Group],
            2 => path.Type == RowPath.Any ? [path.Group] : [path.Group, path.Type],
            _ => path.Family == RowPath.Any ? [path.Group]
                : path.Type == RowPath.Any ? [path.Group, path.Family]
                : [path.Group, path.Family, path.Type]
        };
    }

    public static IEnumerable<int> KeysOf(params CountNode?[] nodes)
    {
        var keys = new HashSet<int>();
        foreach (var node in nodes)
        {
            if (node is not null)
            {
                keys.UnionWith(node.Children.Keys);
            }
        }

        return keys;
    }
}
