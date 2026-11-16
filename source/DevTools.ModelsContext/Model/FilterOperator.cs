using System.ComponentModel;

namespace DevTools.ModelsContext.Model;

public enum FilterOperator
{
    [Description("equals")]
    Equal,
    [Description("does not equal")]
    NotEqual,
    [Description("is greater than")]
    Greater,
    [Description("is greater than or equal to")]
    GreaterOrEqual,
    [Description("is less than")]
    Less,
    [Description("is less than or equal to")]
    LessOrEqual,
    [Description("contains")]
    Contains,
    [Description("does not contain")]
    NotContains,
    [Description("begins with")]
    BeginsWith,
    [Description("does not begin with")]
    NotBeginsWith,
    [Description("ends with")]
    EndsWith,
    [Description("does not end with")]
    NotEndsWith,
    [Description("has a value")]
    HasValue,
    [Description("has no value")]
    HasNoValue,
    [Description("parameter exists")]
    Exists,
    [Description("parameter missing")]
    Missing
}

public static class FilterOperators
{
    private static readonly FilterOperator[] Text =
    [
        FilterOperator.Equal, FilterOperator.NotEqual, FilterOperator.Greater, FilterOperator.GreaterOrEqual,
        FilterOperator.Less, FilterOperator.LessOrEqual, FilterOperator.Contains, FilterOperator.NotContains,
        FilterOperator.BeginsWith, FilterOperator.NotBeginsWith, FilterOperator.EndsWith,
        FilterOperator.NotEndsWith, FilterOperator.HasValue, FilterOperator.HasNoValue, FilterOperator.Exists,
        FilterOperator.Missing
    ];

    private static readonly FilterOperator[] Numeric =
    [
        FilterOperator.Equal, FilterOperator.NotEqual, FilterOperator.Greater, FilterOperator.GreaterOrEqual,
        FilterOperator.Less, FilterOperator.LessOrEqual, FilterOperator.HasValue, FilterOperator.HasNoValue,
        FilterOperator.Exists, FilterOperator.Missing
    ];

    private static readonly FilterOperator[] Equality =
    [
        FilterOperator.Equal, FilterOperator.NotEqual, FilterOperator.HasValue, FilterOperator.HasNoValue,
        FilterOperator.Exists, FilterOperator.Missing
    ];

    public static IReadOnlyList<FilterOperator> For(ValueKind kind)
    {
        return kind switch
        {
            ValueKind.Text => Text,
            ValueKind.Integer or ValueKind.Number => Numeric,
            _ => Equality
        };
    }

    public static bool Allows(ValueKind kind, FilterOperator op)
    {
        return Array.IndexOf((FilterOperator[])For(kind), op) >= 0;
    }

    public static bool NeedsValue(FilterOperator op)
    {
        return op is not (FilterOperator.HasValue or FilterOperator.HasNoValue or FilterOperator.Exists
            or FilterOperator.Missing);
    }

    public static bool IsNegative(FilterOperator op)
    {
        return op is FilterOperator.NotEqual or FilterOperator.NotContains or FilterOperator.NotBeginsWith
            or FilterOperator.NotEndsWith;
    }

    public static bool IsPositiveMatch(FilterOperator op)
    {
        return op is FilterOperator.Equal or FilterOperator.Contains or FilterOperator.BeginsWith
            or FilterOperator.EndsWith;
    }
}

public static class TextRules
{
    public static bool Matches(FilterOperator op, bool exists, string? value, string target, bool caseSensitive)
    {
        if (op == FilterOperator.Missing)
        {
            return !exists;
        }

        if (!exists)
        {
            return false;
        }

        var text = value ?? string.Empty;
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return op switch
        {
            FilterOperator.Exists => true,
            FilterOperator.HasValue => text.Length > 0,
            FilterOperator.HasNoValue => text.Length == 0,
            FilterOperator.Equal => string.Equals(text, target, comparison),
            FilterOperator.NotEqual => !string.Equals(text, target, comparison),
            FilterOperator.Greater => string.Compare(text, target, comparison) > 0,
            FilterOperator.GreaterOrEqual => string.Compare(text, target, comparison) >= 0,
            FilterOperator.Less => string.Compare(text, target, comparison) < 0,
            FilterOperator.LessOrEqual => string.Compare(text, target, comparison) <= 0,
            FilterOperator.Contains => text.IndexOf(target, comparison) >= 0,
            FilterOperator.NotContains => text.IndexOf(target, comparison) < 0,
            FilterOperator.BeginsWith => text.StartsWith(target, comparison),
            FilterOperator.NotBeginsWith => !text.StartsWith(target, comparison),
            FilterOperator.EndsWith => text.EndsWith(target, comparison),
            FilterOperator.NotEndsWith => !text.EndsWith(target, comparison),
            _ => false
        };
    }
}
