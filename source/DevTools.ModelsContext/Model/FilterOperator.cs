namespace DevTools.ModelsContext.Model;

public enum ValueKind
{
    Text,
    Integer,
    Number,
    ElementId
}

public enum FilterOperator
{
    Equal,
    NotEqual,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual,
    Contains,
    NotContains,
    BeginsWith,
    NotBeginsWith,
    EndsWith,
    NotEndsWith,
    HasValue,
    HasNoValue,
    Exists,
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

    private static readonly FilterOperator[] Id =
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
            _ => Id
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
}
