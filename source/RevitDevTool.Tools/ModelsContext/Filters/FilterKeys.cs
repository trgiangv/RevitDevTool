using DevTools.ModelsContext.Filters;
using RuleNode = DevTools.ModelsContext.Filters.FilterRule;

namespace RevitDevTool.Tools.ModelsContext.Filters;

internal static class FilterKeys
{
    public static HashSet<ParameterKey> Presence(FilterNode root)
    {
        var keys = new HashSet<ParameterKey>();
        var pending = new Stack<FilterNode>([root]);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case FilterSet set:
                    foreach (var child in set.Children)
                    {
                        pending.Push(child);
                    }

                    break;
                case RuleNode { Operator: FilterOperator.NotEmpty or FilterOperator.Empty or FilterOperator.Defined or FilterOperator.Undefined } rule:
                    keys.Add(rule.Parameter);
                    break;
            }
        }

        return keys;
    }

    public static List<ParameterKey> All(FilterNode root)
    {
        var keys = new List<ParameterKey>();
        var pending = new Stack<FilterNode>([root]);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case FilterSet set:
                    for (var index = set.Children.Count - 1; index >= 0; index--)
                    {
                        pending.Push(set.Children[index]);
                    }

                    break;
                case RuleNode rule when !keys.Contains(rule.Parameter):
                    keys.Add(rule.Parameter);
                    break;
            }
        }

        return keys;
    }
}
