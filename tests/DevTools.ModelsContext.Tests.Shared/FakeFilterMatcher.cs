using DevTools.ModelsContext.Model;
using System.Globalization;

namespace DevTools.ModelsContext.Tests.Shared;

public static class FakeFilterMatcher
{
    public static bool Matches(
        DocumentSnapshot snapshot,
        ElementRow row,
        FilterNode node,
        IReadOnlyDictionary<long, string> levels,
        double hostUnitFactor = 1)
    {
        return node switch
        {
            FilterSet set => set.Inverted != MatchesSet(snapshot, row, set, levels, hostUnitFactor),
            FilterRule rule => MatchesRule(snapshot, row, rule, levels, hostUnitFactor),
            _ => false
        };
    }

    private static bool MatchesSet(
        DocumentSnapshot snapshot,
        ElementRow row,
        FilterSet set,
        IReadOnlyDictionary<long, string> levels,
        double hostUnitFactor)
    {
        if (set.Children.Count == 0)
        {
            return true;
        }

        return set.Match == SetMatch.All
            ? set.Children.All(child => Matches(snapshot, row, child, levels, hostUnitFactor))
            : set.Children.Any(child => Matches(snapshot, row, child, levels, hostUnitFactor));
    }

    private static bool MatchesRule(
        DocumentSnapshot snapshot,
        ElementRow row,
        FilterRule rule,
        IReadOnlyDictionary<long, string> levels,
        double hostUnitFactor)
    {
        var ids = FakeParameters.IdsOf(snapshot, rule.Parameter);
        var own = FakeParameters.IdOf(snapshot, row, rule.Parameter);
        if (rule.Operator == FilterOperator.Missing)
        {
            return !ids.Any(id => id == own && FakeParameters.TryGet(snapshot, row, rule.Parameter, levels, out _));
        }

        return ids.Any(id => id == own && MatchesId(snapshot, row, rule, levels, hostUnitFactor));
    }

    private static bool MatchesId(
        DocumentSnapshot snapshot,
        ElementRow row,
        FilterRule rule,
        IReadOnlyDictionary<long, string> levels,
        double hostUnitFactor)
    {
        var exists = FakeParameters.TryGet(snapshot, row, rule.Parameter, levels, out var value);
        if (FakeParameters.KindOf(rule.Parameter) != ValueKind.Number || !FilterOperators.NeedsValue(rule.Operator))
        {
            return TextRules.Matches(rule.Operator, exists, value, rule.Value, rule.IsCaseSensitive);
        }

        if (!exists
            || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var actual)
            || !double.TryParse(rule.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var typed))
        {
            return false;
        }

        var compare = actual.CompareTo(typed * hostUnitFactor);
        return rule.Operator switch
        {
            FilterOperator.Equal => compare == 0,
            FilterOperator.NotEqual => compare != 0,
            FilterOperator.Greater => compare > 0,
            FilterOperator.GreaterOrEqual => compare >= 0,
            FilterOperator.Less => compare < 0,
            FilterOperator.LessOrEqual => compare <= 0,
            _ => false
        };
    }
}
