namespace DevTools.ModelsContext.Model;

public sealed record FilterDefinition(IReadOnlyList<long> Groups, FilterSet Root)
{
    public static FilterDefinition Empty { get; } = new([], FilterSet.All());
}

public abstract record FilterNode;

public sealed record FilterSet(SetMatch Match, IReadOnlyList<FilterNode> Children) : FilterNode
{
    public static FilterSet All(params FilterNode[] children)
    {
        return new FilterSet(SetMatch.All, children);
    }

    public static FilterSet Any(params FilterNode[] children)
    {
        return new FilterSet(SetMatch.Any, children);
    }
}

public sealed record FilterRule(ParameterKey Parameter, ValueKind Kind, FilterOperator Operator, string Value = "")
    : FilterNode;

public sealed record UnsupportedRule(string Description) : FilterNode;

public sealed record ParameterKey(string Name, string? BuiltInName = null, Guid? SharedId = null);

public enum SetMatch
{
    All,
    Any
}
