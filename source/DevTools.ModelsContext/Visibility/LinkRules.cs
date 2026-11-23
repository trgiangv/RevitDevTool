using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Filters;

namespace DevTools.ModelsContext.Visibility;

public static class LinkRules
{
    public static ParameterKey FamilyName { get; } = ParameterKey.BuiltIn("Family Name", "ALL_MODEL_FAMILY_NAME");

    public static ParameterKey TypeName { get; } = ParameterKey.BuiltIn("Type Name", "ALL_MODEL_TYPE_NAME");

    public static bool IsRuleRow(TreeRow row)
    {
        return row.Kind is TreeRowKind.Group or TreeRowKind.Family or TreeRowKind.Type;
    }

    public static long CategoryOf(DocumentSnapshot snapshot, TreeRow row)
    {
        return snapshot.Groups[row.Path.Group].GroupId;
    }

    public static FilterNode? RuleOf(DocumentSnapshot snapshot, TreeRow row)
    {
        return row.Kind switch
        {
            TreeRowKind.Family => new FilterRule(FamilyName, FilterOperator.Equal, snapshot.Names[row.Path.Family]),
            TreeRowKind.Type => FilterSet.All(
                new FilterRule(FamilyName, FilterOperator.Equal, snapshot.FamilyName(row.Path.Type)),
                new FilterRule(TypeName, FilterOperator.Equal, snapshot.TypeName(row.Path.Type))),
            _ => null
        };
    }

    public static FilterSet Combine(IReadOnlyList<FilterNode?> rules)
    {
        return rules.Any(rule => rule is null)
            ? FilterSet.All()
            : FilterSet.Any([.. rules.Distinct().Cast<FilterNode>()]);
    }

    public static int CountOf(FilterNode node)
    {
        return node is FilterSet set ? set.Children.Sum(CountOf) : 1;
    }

    public static bool IsNative(FilterNode node)
    {
        return node switch
        {
            FilterSet set => (set.Scope is null || set.Scope.IsSame) && set.Children.All(IsNative),
            FilterRule rule => !rule.IsCaseSensitive && rule.Operator is not (FilterOperator.Defined or FilterOperator.Undefined),
            _ => false
        };
    }

    public static string? Refusal(FilterSet root)
    {
        if (!IsNative(root))
        {
            return VisibilityReasons.LinkRuleNotNative;
        }

        return CountOf(root) > VisibilityReasons.LinkFilterCap ? VisibilityReasons.LinkFilterTooMany : null;
    }
}
