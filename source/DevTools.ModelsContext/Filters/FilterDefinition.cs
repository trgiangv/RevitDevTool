using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Parameters;
using System.ComponentModel;

namespace DevTools.ModelsContext.Filters;

public sealed record FilterDefinition(IReadOnlyList<long> Groups, FilterSet Root)
{
    public static FilterDefinition Empty { get; } = new([], FilterSet.All());
}

public abstract record FilterNode;

public sealed record FilterSet(SetMatch Match, IReadOnlyList<FilterNode> Children, FilterScope? Scope = null) : FilterNode
{
    public static FilterSet All(params FilterNode[] children)
    {
        return new FilterSet(SetMatch.All, children);
    }

    public static FilterSet Any(params FilterNode[] children)
    {
        return new FilterSet(SetMatch.Any, children);
    }

    public FilterSet In(FilterScope scope)
    {
        return this with { Scope = scope };
    }
}

public sealed record FilterScope(IReadOnlyList<SourceId> Documents, IReadOnlyList<long> Categories)
{
    public static FilterScope Same { get; } = new([], []);

    public bool IsSame => Documents.Count == 0 && Categories.Count == 0;

    public static FilterScope Of(IEnumerable<SourceId>? documents = null, IEnumerable<long>? categories = null)
    {
        return new FilterScope((documents ?? []).Distinct().ToList(), (categories ?? []).Distinct().ToList());
    }
}

public sealed record FilterRule(
    ParameterKey Parameter,
    FilterOperator Operator,
    string Value = "",
    bool IsCaseSensitive = false,
    NumberRange? Picked = null) : FilterNode;

public sealed record UnsupportedRule(string Description) : FilterNode;

public sealed record ParameterKey(string Name, string? BuiltInName = null, Guid? SharedId = null, string? DataType = null)
{
    public static ParameterKey BuiltIn(string name, string builtInName)
    {
        return new ParameterKey(name, builtInName);
    }

    public static ParameterKey Shared(string name, Guid id)
    {
        return new ParameterKey(name, null, id);
    }

    public static ParameterKey Named(string name, string? dataType)
    {
        return new ParameterKey(name, null, null, dataType);
    }

    public bool Equals(ParameterKey? other)
    {
        if (other is null)
        {
            return false;
        }

        if (BuiltInName is not null || other.BuiltInName is not null)
        {
            return string.Equals(BuiltInName, other.BuiltInName, StringComparison.Ordinal);
        }

        if (SharedId is not null || other.SharedId is not null)
        {
            return SharedId == other.SharedId;
        }

        return string.Equals(Name, other.Name, StringComparison.Ordinal)
               && string.Equals(DataType, other.DataType, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        if (BuiltInName is { } builtIn)
        {
            return StringComparer.Ordinal.GetHashCode(builtIn);
        }

        if (SharedId is { } shared)
        {
            return shared.GetHashCode();
        }

        unchecked
        {
            return StringComparer.Ordinal.GetHashCode(Name) * 397 ^ StringComparer.Ordinal.GetHashCode(DataType ?? string.Empty);
        }
    }
}

public enum SetMatch
{
    [Description("AND")]
    All,
    [Description("OR")]
    Any
}
