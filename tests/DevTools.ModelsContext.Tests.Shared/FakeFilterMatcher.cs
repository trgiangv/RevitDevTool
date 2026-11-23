using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
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
            FilterSet set => InCategories(snapshot, row, set.Scope) && MatchesSet(snapshot, row, set, levels, hostUnitFactor),
            FilterRule rule => MatchesRule(snapshot, row, rule, levels, hostUnitFactor),
            _ => false
        };
    }

    private static bool InCategories(DocumentSnapshot snapshot, ElementRow row, FilterScope? scope)
    {
        return scope is not { Categories.Count: > 0 } || scope.Categories.Contains(snapshot.Groups[row.GroupIndex].GroupId);
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
        if (rule.Operator == FilterOperator.Undefined)
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

        if (!exists || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var actual))
        {
            return false;
        }

        if (rule.Picked is { } picked)
        {
            return rule.Operator switch
            {
                FilterOperator.Equal => actual >= picked.Min && actual <= picked.Max,
                FilterOperator.NotEqual => actual < picked.Min || actual > picked.Max,
                FilterOperator.Greater => actual > picked.Max,
                FilterOperator.GreaterOrEqual => actual >= picked.Min,
                FilterOperator.Less => actual < picked.Min,
                FilterOperator.LessOrEqual => actual <= picked.Max,
                _ => false
            };
        }

        if (!double.TryParse(rule.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var typed))
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
