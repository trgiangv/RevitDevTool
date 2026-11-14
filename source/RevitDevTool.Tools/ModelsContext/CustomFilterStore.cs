using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using DevTools.Settings;
using System.Text.Json.Serialization;
using RuleNode = DevTools.ModelsContext.Model.FilterRule;

namespace RevitDevTool.Tools.ModelsContext;

public sealed class CustomFilterStore(IFileConfig<PathOptions> fileConfig) : ICustomFilterStore
{
    public IReadOnlyList<CustomFilter> Load()
    {
        try
        {
            return (fileConfig.Load<ModelContextFilters>()?.Filters ?? [])
                .Where(filter => !string.IsNullOrWhiteSpace(filter.Name))
                .Select(filter => new CustomFilter(filter.Name,
                    new FilterDefinition(filter.Groups, ToSet(filter.Root))))
                .ToList();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }
    }

    public void Save(IReadOnlyList<CustomFilter> filters)
    {
        try
        {
            fileConfig.Save(new ModelContextFilters
            {
                Filters = filters.Select(filter => new StoredFilter
                {
                    Name = filter.Name,
                    Groups = [.. filter.Definition.Groups],
                    Root = FromNode(filter.Definition.Root)
                }).ToList()
            });
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }
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
                children.Select(ToNode).OfType<FilterNode>().ToList());
        }

        if (node.Parameter is not { } parameter
            || !Enum.TryParse<ValueKind>(node.Kind, out var kind)
            || !Enum.TryParse<FilterOperator>(node.Operator, out var op))
        {
            return null;
        }

        return new RuleNode(new ParameterKey(parameter.Name, parameter.BuiltInName, parameter.SharedId), kind, op,
            node.Value ?? string.Empty);
    }

    private static StoredNode? FromNode(FilterNode node)
    {
        return node switch
        {
            FilterSet set => new StoredNode
            {
                Any = set.Match == SetMatch.Any,
                Children = set.Children.Select(FromNode).OfType<StoredNode>().ToList()
            },
            RuleNode rule => new StoredNode
            {
                Parameter = new StoredParameter
                {
                    Name = rule.Parameter.Name,
                    BuiltInName = rule.Parameter.BuiltInName,
                    SharedId = rule.Parameter.SharedId
                },
                Kind = rule.Kind.ToString(),
                Operator = rule.Operator.ToString(),
                Value = rule.Value
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

    [JsonPropertyName("children")]
    public List<StoredNode>? Children { get; set; }

    [JsonPropertyName("parameter")]
    public StoredParameter? Parameter { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("operator")]
    public string? Operator { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
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
}
