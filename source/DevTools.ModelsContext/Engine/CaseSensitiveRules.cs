using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Engine;

public static class CaseSensitiveRules
{
    private static readonly FilterSet MatchNone = FilterSet.Not(SetMatch.All);

    public static bool IsCaseSensitive(FilterRule rule)
    {
        return rule.IsCaseSensitive && FilterOperators.NeedsValue(rule.Operator);
    }

    public static bool Any(FilterNode node)
    {
        return node switch
        {
            FilterSet set => set.Children.Any(Any),
            FilterRule rule => IsCaseSensitive(rule),
            _ => false
        };
    }

    public static FilterSet Widen(FilterSet set)
    {
        return WidenSet(set, true);
    }

    private static FilterSet WidenSet(FilterSet set, bool positive)
    {
        var inner = set.Inverted ? !positive : positive;
        return set with { Children = set.Children.Select(child => Widen(child, inner)).ToList() };
    }

    private static FilterNode Widen(FilterNode node, bool positive)
    {
        return node switch
        {
            FilterSet set => WidenSet(set, positive),
            FilterRule rule when IsCaseSensitive(rule) => WidenRule(rule, positive),
            _ => node
        };
    }

    private static FilterNode WidenRule(FilterRule rule, bool positive)
    {
        var insensitive = rule with { IsCaseSensitive = false };
        if (positive)
        {
            return FilterOperators.IsPositiveMatch(rule.Operator)
                ? insensitive
                : new FilterRule(rule.Parameter, FilterOperator.Exists);
        }

        return FilterOperators.IsNegative(rule.Operator) ? insensitive : MatchNone;
    }
}
