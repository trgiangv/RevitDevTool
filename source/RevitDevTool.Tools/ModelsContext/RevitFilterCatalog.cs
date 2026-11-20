using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using Nice3point.Revit.Extensions;
using System.Globalization;
using RevitFilterRule = Autodesk.Revit.DB.FilterRule;
using RuleNode = DevTools.ModelsContext.Model.FilterRule;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class RevitFilterCatalog : IHostFilterCatalog
{
    public const string GlobalParameterRule = "Global parameter association";
    public const string InvertedRule = "Inverted rule without a negated operator";

    public Task<IReadOnlyList<HostFilterInfo>> GetFiltersAsync(DocumentKey document, CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync<IReadOnlyList<HostFilterInfo>>("Host filters", application =>
        {
            var resolved = DocumentRegistry.Require(application, document);
            var inView = FiltersInView(application, document, resolved);
            var filters = new List<HostFilterInfo>();
            foreach (var element in resolved.Target.CollectElements().OfClass<FilterElement>())
            {
                var kind = element switch
                {
                    ParameterFilterElement => HostFilterKind.RuleBased,
                    SelectionFilterElement => HostFilterKind.Selection,
                    _ => (HostFilterKind?)null
                };
                if (kind is { } filterKind)
                {
                    var id = element.Id.ToLong();
                    filters.Add(new HostFilterInfo(document, id, element.Name, filterKind, inView.Contains(id)));
                }
            }

            return filters.OrderBy(filter => filter.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }, cancellationToken);
    }

    public Task<FilterDefinition?> ReadAsync(DocumentKey document, long filterId, CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Read host filter", application =>
        {
            var target = DocumentRegistry.Require(application, document).Target;
            if (target.GetElement(filterId.ToElementId()) is not ParameterFilterElement filter)
            {
                return null;
            }

            var categories = filter.GetCategories();
            var reader = new RuleReader(target, categories);
            var root = filter.GetElementFilter() is { } elementFilter ? reader.Read(elementFilter) : null;
            return new FilterDefinition(categories.Select(id => id.ToLong()).ToList(), root switch
            {
                FilterSet set => set,
                null => FilterSet.All(),
                _ => FilterSet.All(root)
            });
        }, cancellationToken);
    }

    public async Task<IReadOnlyCollection<long>> EvaluateAsync(
        DocumentKey document,
        long filterId,
        IReadOnlyCollection<long>? scope,
        CancellationToken cancellationToken)
    {
        var (label, candidates) = await RevitProfiler.RunAsync("Host filter candidates", application =>
        {
            var resolved = DocumentRegistry.Require(application, document);
            var target = resolved.Target;
            var candidates = target.GetElement(filterId.ToElementId()) switch
            {
                ParameterFilterElement rule when rule.GetCategories() is { Count: > 0 } categories =>
                    ElementReader.CandidatesOf(target, categories),
                SelectionFilterElement selection => ElementReader.CandidatesAmong(target, selection.GetElementIds()),
                _ => new CandidateSet([], [])
            };
            return (DocumentRegistry.SourceOf(resolved).Label, candidates.InScope(scope));
        }, cancellationToken).ConfigureAwait(false);
        return await RevitFilterEvaluator.MatchInSlicesAsync(document, label, candidates,
            (target, chunk) => Match(target, chunk, filterId), cancellationToken).ConfigureAwait(false);
    }

    private static List<long> Match(Document target, List<ElementId> chunk, long filterId)
    {
        return target.GetElement(filterId.ToElementId()) switch
        {
            ParameterFilterElement rule when rule.GetElementFilter() is { } elementFilter =>
                target.CollectElements(chunk).WherePasses(elementFilter).ToElementIds().Select(id => id.ToLong()).ToList(),
            ParameterFilterElement or SelectionFilterElement => chunk.Select(id => id.ToLong()).ToList(),
            _ => []
        };
    }

    private static HashSet<long> FiltersInView(UIApplication application, DocumentKey document, ResolvedDocument resolved)
    {
        if (!DocumentRegistry.IsActive(application, document) || application.ActiveUIDocument?.ActiveView is not { } view)
        {
            return [];
        }

        try
        {
            if (resolved.Link is null)
            {
                return [.. view.GetFilters().Select(id => id.ToLong())];
            }

            return LinkedViewFilters(view, resolved);
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "View filters", document);
            return [];
        }
    }

    private static HashSet<long> LinkedViewFilters(View view, ResolvedDocument resolved)
    {
#if REVIT2024_OR_GREATER
        var settings = view.GetLinkOverrides(resolved.Link!.Id);
        if (settings is not { LinkVisibilityType: LinkVisibility.ByLinkView }
            || resolved.Target.GetElement(settings.LinkedViewId) is not View linkedView)
        {
            return [];
        }

        return [.. linkedView.GetFilters().Select(id => id.ToLong())];
#else
        return [];
#endif
    }

    private sealed class RuleReader(Document document, ICollection<ElementId> categories)
    {
        public FilterNode Read(ElementFilter filter)
        {
            return filter switch
            {
                LogicalAndFilter and => new FilterSet(SetMatch.All, and.GetFilters().Select(Read).ToList()),
                LogicalOrFilter or => new FilterSet(SetMatch.Any, or.GetFilters().Select(Read).ToList()),
                ElementParameterFilter { Inverted: true } parameters => ReadInverted(parameters.GetRules()),
                ElementParameterFilter parameters => ReadRules(parameters.GetRules()),
                _ => new UnsupportedRule(filter.GetType().Name)
            };
        }

        private FilterNode ReadRules(IList<RevitFilterRule> rules)
        {
            var nodes = rules.Select(rule => ReadRule(rule, false)).ToList();
            return nodes.Count == 1 ? nodes[0] : new FilterSet(SetMatch.All, nodes);
        }

        private FilterNode ReadInverted(IList<RevitFilterRule> rules)
        {
            var nodes = rules.Select(rule => ReadRule(rule, true)).ToList();
            if (nodes.Any(node => node is not RuleNode))
            {
                return new UnsupportedRule(InvertedRule);
            }

            return nodes.Count == 1 ? nodes[0] : new FilterSet(SetMatch.Any, nodes);
        }

        private FilterNode ReadRule(RevitFilterRule rule, bool negated)
        {
            switch (rule)
            {
                case FilterInverseRule inverse:
                    return ReadRule(inverse.GetInnerRule(), !negated);
                case FilterGlobalParameterAssociationRule:
                    return new UnsupportedRule(GlobalParameterRule);
            }

            var parameterId = rule.GetRuleParameter();
            var key = ParameterLocator.KeyOf(document, parameterId);
            return rule switch
            {
                HasValueFilterRule => new RuleNode(key, negated ? FilterOperator.Empty : FilterOperator.NotEmpty),
                HasNoValueFilterRule => new RuleNode(key, negated ? FilterOperator.NotEmpty : FilterOperator.Empty),
                FilterStringRule text when StringOperator(text.GetEvaluator(), negated) is { } op =>
                    new RuleNode(key, op, text.RuleString),
                FilterIntegerRule integer when NumericOperator(integer.GetEvaluator(), negated) is { } op =>
                    new RuleNode(key, op, integer.RuleValue.ToString(CultureInfo.InvariantCulture)),
                FilterDoubleRule number when NumericOperator(number.GetEvaluator(), negated) is { } op =>
                    new RuleNode(key, op, FormatNumber(key, number.RuleValue), Picked: NumberRange.Of(number.RuleValue)),
                FilterElementIdRule reference when NumericOperator(reference.GetEvaluator(), negated) is
                { } op and (FilterOperator.Equal or FilterOperator.NotEqual) =>
                    new RuleNode(key, op, ReferenceText(reference.RuleValue)),
                _ => new UnsupportedRule(rule.GetType().Name)
            };
        }

        private string FormatNumber(ParameterKey key, double value)
        {
            try
            {
                if (ParameterLocator.Locate(document, key, categories).FirstOrDefault(found => found.Spec is not null)?.Spec is { } spec
                    && SpecInfo.IsMeasurable(spec))
                {
                    return UnitFormatUtils.Format(document.GetUnits(), spec, value, false);
                }
            }
            catch (Exception exception)
            {
                RevitProfiler.Logger.Swallowed(exception, "Format number", key.Name);
            }

            return value.ToString(CultureInfo.CurrentCulture);
        }

        private string ReferenceText(ElementId id)
        {
            return document.GetElement(id)?.Name ?? Category.GetCategory(document, id)?.Name ?? id.ToString();
        }

        private static FilterOperator? StringOperator(FilterStringRuleEvaluator evaluator, bool negated)
        {
            FilterOperator? op = evaluator switch
            {
                FilterStringEquals => FilterOperator.Equal,
                FilterStringContains => FilterOperator.Contains,
                FilterStringBeginsWith => FilterOperator.BeginsWith,
                FilterStringEndsWith => FilterOperator.EndsWith,
                FilterStringGreater => FilterOperator.Greater,
                FilterStringGreaterOrEqual => FilterOperator.GreaterOrEqual,
                FilterStringLess => FilterOperator.Less,
                FilterStringLessOrEqual => FilterOperator.LessOrEqual,
                _ => null
            };
            return Negate(op, negated);
        }

        private static FilterOperator? NumericOperator(FilterNumericRuleEvaluator evaluator, bool negated)
        {
            FilterOperator? op = evaluator switch
            {
                FilterNumericEquals => FilterOperator.Equal,
                FilterNumericGreater => FilterOperator.Greater,
                FilterNumericGreaterOrEqual => FilterOperator.GreaterOrEqual,
                FilterNumericLess => FilterOperator.Less,
                FilterNumericLessOrEqual => FilterOperator.LessOrEqual,
                _ => null
            };
            return Negate(op, negated);
        }

        private static FilterOperator? Negate(FilterOperator? op, bool negated)
        {
            if (!negated || op is not { } positive)
            {
                return op;
            }

            return positive switch
            {
                FilterOperator.Equal => FilterOperator.NotEqual,
                FilterOperator.Contains => FilterOperator.NotContains,
                FilterOperator.BeginsWith => FilterOperator.NotBeginsWith,
                FilterOperator.EndsWith => FilterOperator.NotEndsWith,
                FilterOperator.Greater => FilterOperator.LessOrEqual,
                FilterOperator.GreaterOrEqual => FilterOperator.Less,
                FilterOperator.Less => FilterOperator.GreaterOrEqual,
                FilterOperator.LessOrEqual => FilterOperator.Greater,
                _ => positive
            };
        }
    }
}
