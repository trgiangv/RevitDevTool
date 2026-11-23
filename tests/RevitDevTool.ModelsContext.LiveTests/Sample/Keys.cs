using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Scope;
using RuleNode = DevTools.ModelsContext.Filters.FilterRule;

namespace RevitDevTool.ModelsContext.LiveTests;

internal static class Keys
{
    public static ParameterKey Of(string alias)
    {
        if (!CaseFiles.Facts.Parameters.TryGetValue(alias, out var data))
        {
            Assert.Fail($"sample-facts.json has no parameter '{alias}'.");
        }

        if (data!.BuiltIn is { } builtIn)
        {
            return BuiltIn((BuiltInParameter)Enum.Parse(typeof(BuiltInParameter), builtIn));
        }

        if (data.SharedFirstIn is { } docs)
        {
            return FirstShared(SampleModel.Of(docs)[0].Document);
        }

        return ParameterKey.Named(data.Name ?? alias, data.Spec is null ? null : Oracle.WithoutVersion(data.Spec));
    }

    public static BuiltInParameter BuiltInOf(string name)
    {
        return (BuiltInParameter)Enum.Parse(typeof(BuiltInParameter), name);
    }

    public static ParameterKey BuiltIn(BuiltInParameter parameter)
    {
        return ParameterKey.BuiltIn(LabelUtils.GetLabelFor(parameter), parameter.ToString());
    }

    public static ParameterKey FirstShared(Document document)
    {
        var elements = Oracle.Candidates(document, []).ToList();
        foreach (var shared in new FilteredElementCollector(document)
                     .OfClass(typeof(SharedParameterElement))
                     .Cast<SharedParameterElement>()
                     .OrderBy(element => element.Name, StringComparer.Ordinal))
        {
            var key = ParameterKey.Shared(shared.Name, shared.GuidValue);
            if (elements.Any(element => Oracle.Read(element, key) is not (null, null)))
            {
                return key;
            }
        }

        Assert.Inconclusive($"No shared parameter is on a model element in {document.Title}.");
        return null!;
    }
}

internal static class Cases
{
    public static FilterDefinition Definition(FilterCaseData item, Func<ParameterKey, IReadOnlyList<long>, string, NumberRange?>? pick = null)
    {
        var groups = Categories(item.Filter.Categories);
        return new FilterDefinition(groups, Set(item.Filter, false, (key, text) => pick?.Invoke(key, groups, text)));
    }

    public static ScopeKind Scope(FilterCaseData item)
    {
        return (ScopeKind)Enum.Parse(typeof(ScopeKind), item.Scope);
    }

    public static long Category(string name)
    {
        return (long)(BuiltInCategory)Enum.Parse(typeof(BuiltInCategory), name);
    }

    public static List<long> Categories(IEnumerable<string>? names)
    {
        return (names ?? []).Select(Category).ToList();
    }

    public static RuleNode Rule(string parameter, FilterOperator op, string value = "")
    {
        return new RuleNode(Keys.Of(parameter), op, value);
    }

    private static FilterSet Set(NodeData node, bool scoped, Func<ParameterKey, string, NumberRange?> pick)
    {
        var match = node.Match is null ? SetMatch.All : (SetMatch)Enum.Parse(typeof(SetMatch), node.Match);
        return new FilterSet(match, (node.Rules ?? []).Select(child => Node(child, pick)).ToList(), scoped ? ScopeOf(node.Scope) : null);
    }

    private static FilterNode Node(NodeData node, Func<ParameterKey, string, NumberRange?> pick)
    {
        if (node.IsGroup)
        {
            return Set(node, true, pick);
        }

        var key = Keys.Of(node.Parameter!);
        return new RuleNode(key, (FilterOperator)Enum.Parse(typeof(FilterOperator), node.Operator!), node.Value,
            node.CaseSensitive, node.Picked ? pick(key, node.Value) ?? throw new InvalidOperationException(
                $"The value list of '{key.Name}' has no '{node.Value}' to pick.") : null);
    }

    private static FilterScope? ScopeOf(ScopeData? scope)
    {
        return scope is null ? null : FilterScope.Of(scope.Documents.Select(SampleModel.SourceOf), Categories(scope.Categories));
    }
}
