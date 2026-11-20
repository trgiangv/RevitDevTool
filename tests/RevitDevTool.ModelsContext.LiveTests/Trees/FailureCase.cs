using DevTools.ModelsContext.Model;
using System.Text.Json;
using RuleNode = DevTools.ModelsContext.Model.FilterRule;

namespace RevitDevTool.ModelsContext.LiveTests;

internal static class FailureCase
{
    public static string Write(string id, int seed, string docs, FilterDefinition definition, int expected, string difference)
    {
        var parameters = new Dictionary<string, object>(StringComparer.Ordinal);
        var item = new FilterCaseData
        {
            Id = id,
            Description = $"Random tree, seed {seed}: {difference}",
            Docs = docs,
            Scope = "WholeModel",
            Expected = JsonSerializer.SerializeToElement(expected),
            ManualSteps =
            [
                "Open the Snowdon HVAC sample with the Architectural link as for any filters.json case.",
                "Build the tree of this case in the Filter tab (the stored form below can be pasted into the custom filter file).",
                "Compare the number before 'matches' with the expected value, which is the oracle count."
            ],
            Filter = Root(definition, parameters)
        };
        var stored = StoredFilters.Write([new CustomFilter(id, definition)]);
        var path = Path.Combine(Evidence.Folder, $"random-failure-{id}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { @case = item, parameters, stored }, CaseFiles.Options));
        return path;
    }

    private static NodeData Root(FilterDefinition definition, Dictionary<string, object> parameters)
    {
        var root = Group(definition.Root, parameters);
        root.Categories = [.. definition.Groups.Select(Category)];
        return root;
    }

    private static NodeData Group(FilterSet set, Dictionary<string, object> parameters)
    {
        return new NodeData
        {
            Match = set.Match.ToString(),
            Scope = set.Scope is { } scope
                ? new ScopeData { Documents = [.. scope.Documents.Select(Source)], Categories = [.. scope.Categories.Select(Category)] }
                : null,
            Rules = [.. set.Children.Select(child => child switch
            {
                FilterSet nested => Group(nested, parameters),
                RuleNode rule => Rule(rule, parameters),
                _ => throw new InvalidOperationException("Random trees hold groups and rules only.")
            })]
        };
    }

    private static NodeData Rule(RuleNode rule, Dictionary<string, object> parameters)
    {
        var key = rule.Parameter;
        var alias = key.BuiltInName is { } builtIn
            ? $"{key.Name} ({builtIn})"
            : key.SharedId is not null ? $"{key.Name} (shared)" : $"{key.Name} ({key.DataType})";
        parameters[alias] = key.BuiltInName is { } name
            ? new { builtIn = name }
            : key.SharedId is { } shared
                ? new { name = key.Name, sharedId = shared }
                : (object)new { name = key.Name, spec = key.DataType };
        return new NodeData
        {
            Parameter = alias,
            Operator = rule.Operator.ToString(),
            Value = rule.Value,
            CaseSensitive = rule.IsCaseSensitive,
            Picked = rule.Picked is not null
        };
    }

    private static string Category(long id)
    {
        return ((BuiltInCategory)(int)id).ToString();
    }

    private static string Source(SourceId source)
    {
        if (source.Equals(SampleModel.Current.Source))
        {
            return "Host";
        }

        return source.Equals(SampleModel.Architectural.Source) ? "Arch" : source.Value;
    }
}
