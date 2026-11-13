using DevTools.ModelsContext.Model;
using System.Globalization;

namespace DevTools.ModelsContext.Tests.Shared;

public static class FakeFilterMatcher
{
    public static bool Matches(
        DocumentSnapshot snapshot,
        ElementRow row,
        FilterNode node,
        IReadOnlyDictionary<long, string> levels)
    {
        return node switch
        {
            FilterSet set when set.Children.Count == 0 => true,
            FilterSet { Match: SetMatch.All } set => set.Children.All(child =>
                Matches(snapshot, row, child, levels)),
            FilterSet set => set.Children.Any(child => Matches(snapshot, row, child, levels)),
            FilterRule rule => MatchesRule(snapshot, row, rule, levels),
            _ => false
        };
    }

    private static bool MatchesRule(
        DocumentSnapshot snapshot,
        ElementRow row,
        FilterRule rule,
        IReadOnlyDictionary<long, string> levels)
    {
        var exists = FakeParameters.TryGet(snapshot, row, rule.Parameter, levels, out var value);
        switch (rule.Operator)
        {
            case FilterOperator.Exists:
                return exists;
            case FilterOperator.Missing:
                return !exists;
            case FilterOperator.HasValue:
                return exists && value.Length > 0;
            case FilterOperator.HasNoValue:
                return exists && value.Length == 0;
        }

        if (!exists)
        {
            return false;
        }

        var compare = Compare(rule.Kind, value, rule.Value);
        return rule.Operator switch
        {
            FilterOperator.Equal => compare == 0,
            FilterOperator.NotEqual => compare != 0,
            FilterOperator.Greater => compare > 0,
            FilterOperator.GreaterOrEqual => compare >= 0,
            FilterOperator.Less => compare < 0,
            FilterOperator.LessOrEqual => compare <= 0,
            FilterOperator.Contains => Index(value, rule.Value) >= 0,
            FilterOperator.NotContains => Index(value, rule.Value) < 0,
            FilterOperator.BeginsWith => value.StartsWith(rule.Value, StringComparison.OrdinalIgnoreCase),
            FilterOperator.NotBeginsWith => !value.StartsWith(rule.Value, StringComparison.OrdinalIgnoreCase),
            FilterOperator.EndsWith => value.EndsWith(rule.Value, StringComparison.OrdinalIgnoreCase),
            FilterOperator.NotEndsWith => !value.EndsWith(rule.Value, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static int Index(string value, string part)
    {
        return value.IndexOf(part, StringComparison.OrdinalIgnoreCase);
    }

    private static int Compare(ValueKind kind, string left, string right)
    {
        if (kind is ValueKind.Integer or ValueKind.Number
            && double.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
            && double.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
        {
            return a.CompareTo(b);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
