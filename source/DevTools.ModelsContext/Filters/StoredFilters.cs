using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Parameters;
using System.Text.Json.Serialization;

namespace DevTools.ModelsContext.Filters;

public static class StoredFilters
{
    public static IReadOnlyList<CustomFilter> Read(ModelContextFilters? stored)
    {
        return (stored?.Filters ?? [])
            .Where(filter => !string.IsNullOrWhiteSpace(filter.Name))
            .Select(filter => new CustomFilter(filter.Name, new FilterDefinition(filter.Groups, ToSet(filter.Root))))
            .ToList();
    }

    public static ModelContextFilters Write(IReadOnlyList<CustomFilter> filters)
    {
        return new ModelContextFilters
        {
            Filters = filters.Select(filter => new StoredFilter
            {
                Name = filter.Name,
                Groups = [.. filter.Definition.Groups],
                Root = FromNode(filter.Definition.Root)
            }).ToList()
        };
    }

    private static FilterSet ToSet(StoredNode? node)
    {
        return node is null ? FilterSet.All() : ToNode(node) as FilterSet ?? FilterSet.All();
    }

    private static FilterNode? ToNode(StoredNode node)
    {
        if (node.Children is { } children)
        {
            return new FilterSet(node.Any ? SetMatch.Any : SetMatch.All,
                children.Select(ToNode).OfType<FilterNode>().ToList(), ToScope(node.Scope));
        }

        if (node.Parameter is not { } parameter || !Enum.TryParse<FilterOperator>(node.Operator, out var op))
        {
            return null;
        }

        return new FilterRule(new ParameterKey(parameter.Name, parameter.BuiltInName, parameter.SharedId, parameter.DataType),
            op, node.Value ?? string.Empty, node.CaseSensitive,
            node.Picked is { Count: 2 } picked ? new NumberRange(picked[0], picked[1]) : null);
    }

    private static FilterScope? ToScope(StoredScope? scope)
    {
        return scope is null ? null : FilterScope.Of(scope.Documents.Select(id => new SourceId(id)), scope.Categories);
    }

    private static StoredScope? FromScope(FilterScope? scope)
    {
        return scope is null || scope.IsSame
            ? null
            : new StoredScope
            {
                Documents = scope.Documents.Select(source => source.Value).ToList(),
                Categories = [.. scope.Categories]
            };
    }

    private static StoredNode? FromNode(FilterNode node)
    {
        return node switch
        {
            FilterSet set => new StoredNode
            {
                Any = set.Match == SetMatch.Any,
                Scope = FromScope(set.Scope),
                Children = set.Children.Select(FromNode).OfType<StoredNode>().ToList()
            },
            FilterRule rule => new StoredNode
            {
                Parameter = new StoredParameter
                {
                    Name = rule.Parameter.Name,
                    BuiltInName = rule.Parameter.BuiltInName,
                    SharedId = rule.Parameter.SharedId,
                    DataType = rule.Parameter.DataType
                },
                Operator = rule.Operator.ToString(),
                Value = rule.Value,
                CaseSensitive = rule.IsCaseSensitive,
                Picked = rule.Picked is { } picked ? [picked.Min, picked.Max] : null
            },
            _ => null
        };
    }
}

[Serializable]
public sealed class ModelContextFilters
{
    [JsonPropertyName("filters")]
    public List<StoredFilter> Filters { get; set; } = [];
}

[Serializable]
public sealed class StoredFilter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("groups")]
    public List<long> Groups { get; set; } = [];

    [JsonPropertyName("root")]
    public StoredNode? Root { get; set; }
}

[Serializable]
public sealed class StoredNode
{
    [JsonPropertyName("any")]
    public bool Any { get; set; }

    [JsonPropertyName("scope")]
    public StoredScope? Scope { get; set; }

    [JsonPropertyName("children")]
    public List<StoredNode>? Children { get; set; }

    [JsonPropertyName("parameter")]
    public StoredParameter? Parameter { get; set; }

    [JsonPropertyName("operator")]
    public string? Operator { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("caseSensitive")]
    public bool CaseSensitive { get; set; }

    [JsonPropertyName("picked")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<double>? Picked { get; set; }
}

[Serializable]
public sealed class StoredScope
{
    [JsonPropertyName("documents")]
    public List<string> Documents { get; set; } = [];

    [JsonPropertyName("categories")]
    public List<long> Categories { get; set; } = [];
}

[Serializable]
public sealed class StoredParameter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("builtInName")]
    public string? BuiltInName { get; set; }

    [JsonPropertyName("sharedId")]
    public Guid? SharedId { get; set; }

    [JsonPropertyName("dataType")]
    public string? DataType { get; set; }
}
