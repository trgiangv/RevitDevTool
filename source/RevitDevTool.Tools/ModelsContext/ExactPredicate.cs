using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using RuleNode = DevTools.ModelsContext.Model.FilterRule;

namespace RevitDevTool.Tools.ModelsContext;

internal static class ExactPredicate
{
    public static Func<Element, bool> Build(
        FilterNode node,
        Func<ParameterKey, IReadOnlyList<ResolvedParameter>> resolve,
        Func<RuleNode, Compiled> leaf)
    {
        return node switch
        {
            FilterSet set => ForSet(set, resolve, leaf),
            RuleNode rule when TextIdsOf(rule, resolve) is { Length: > 0 } ids => ForText(rule, ids),
            RuleNode rule => leaf(rule).Passes,
            _ => static _ => true
        };
    }

    private static Func<Element, bool> ForSet(
        FilterSet set,
        Func<ParameterKey, IReadOnlyList<ResolvedParameter>> resolve,
        Func<RuleNode, Compiled> leaf)
    {
        var children = set.Children.Select(child => Build(child, resolve, leaf)).ToArray();
        var scope = RevitRuleFactory.InCategories(set.Scope);
        return set.Match == SetMatch.All
            ? element => scope.Passes(element) && AllPass(children, element)
            : element => scope.Passes(element) && AnyPasses(children, element);
    }

    private static Func<Element, bool> ForText(RuleNode rule, ElementId[] ids)
    {
        return element =>
        {
            var value = FirstFound(element, rule.Parameter, ids);
            return TextRules.Matches(rule.Operator, value is not null, value?.AsString(), rule.Value, rule.IsCaseSensitive);
        };
    }

    private static ElementId[] TextIdsOf(RuleNode rule, Func<ParameterKey, IReadOnlyList<ResolvedParameter>> resolve)
    {
        if (!CaseSensitiveRules.IsCaseSensitive(rule) && !TextRules.IsOrder(rule.Operator))
        {
            return [];
        }

        return resolve(rule.Parameter)
            .Where(parameter => parameter.Storage == StorageType.String)
            .Select(parameter => parameter.ElementId)
            .ToArray();
    }

    private static bool AllPass(Func<Element, bool>[] children, Element element)
    {
        foreach (var child in children)
        {
            if (!child(element))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AnyPasses(Func<Element, bool>[] children, Element element)
    {
        if (children.Length == 0)
        {
            return true;
        }

        foreach (var child in children)
        {
            if (child(element))
            {
                return true;
            }
        }

        return false;
    }

    private static Parameter? FirstFound(Element element, ParameterKey key, ElementId[] ids)
    {
        foreach (var id in ids)
        {
            if (ParameterLocator.Find(element, key, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
