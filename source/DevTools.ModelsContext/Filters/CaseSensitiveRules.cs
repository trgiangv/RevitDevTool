namespace DevTools.ModelsContext.Filters;

public static class CaseSensitiveRules
{
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
        return set with { Children = set.Children.Select(Widen).ToList() };
    }

    private static FilterNode Widen(FilterNode node)
    {
        return node switch
        {
            FilterSet set => Widen(set),
            FilterRule rule when IsCaseSensitive(rule) => WidenRule(rule),
            _ => node
        };
    }

    private static FilterNode WidenRule(FilterRule rule)
    {
        return FilterOperators.IsPositiveMatch(rule.Operator)
            ? rule with { IsCaseSensitive = false }
            : new FilterRule(rule.Parameter, FilterOperator.Defined);
    }
}
