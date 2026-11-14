using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using System.Globalization;
using RevitFilterRule = Autodesk.Revit.DB.FilterRule;
using RuleNode = DevTools.ModelsContext.Model.FilterRule;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class RevitFilterCatalog : IHostFilterCatalog
{
    public const string GlobalParameterRule = "Global parameter association";
    public const string InvertedFilter = "Inverted parameter filter";

    public Task<IReadOnlyList<HostFilterInfo>> GetFiltersAsync(DocumentKey document, CancellationToken cancellationToken)
    {
        return RevitCall.RunAsync<IReadOnlyList<HostFilterInfo>>(application =>
        {
            var resolved = DocumentRegistry.Require(application, document);
            var inView = FiltersInView(application, document, resolved);
            var filters = new List<HostFilterInfo>();
            foreach (var element in new FilteredElementCollector(resolved.Target).OfClass(typeof(FilterElement)))
            {
                var kind = element switch
                {
                    ParameterFilterElement => HostFilterKind.RuleBased,
                    SelectionFilterElement => HostFilterKind.Selection,
                    _ => (HostFilterKind?)null
                };
                if (kind is { } filterKind)
                {
                    var id = RevitIds.ValueOf(element.Id);
                    filters.Add(new HostFilterInfo(document, id, element.Name, filterKind, inView.Contains(id)));
                }
            }

            return filters.OrderBy(filter => filter.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }, cancellationToken);
    }

    public Task<FilterDefinition?> ReadAsync(DocumentKey document, long filterId, CancellationToken cancellationToken)
    {
        return RevitCall.RunAsync(application =>
        {
            var target = DocumentRegistry.Require(application, document).Target;
            if (target.GetElement(RevitIds.From(filterId)) is not ParameterFilterElement filter)
            {
                return null;
            }

            var categories = filter.GetCategories();
            var reader = new RuleReader(target, categories);
            var root = filter.GetElementFilter() is { } elementFilter ? reader.Read(elementFilter) : null;
            return new FilterDefinition(RevitIds.ValuesOf(categories), root switch
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
        var candidates = await RevitCall.RunAsync(application =>
        {
            var target = DocumentRegistry.Require(application, document).Target;
            var ids = target.GetElement(RevitIds.From(filterId)) switch
            {
                ParameterFilterElement rule when rule.GetCategories() is { Count: > 0 } categories =>
                    ElementReader.CollectIds(target, categories),
                SelectionFilterElement selection => RevitIds.ValuesOf(selection.GetElementIds()).ToArray(),
                _ => []
            };
            return RevitFilterEvaluator.InScope(ids, scope);
        }, cancellationToken).ConfigureAwait(false);
        return await RevitFilterEvaluator.MatchInSlicesAsync(document, candidates,
            (target, chunk) => Match(target, chunk, filterId), cancellationToken).ConfigureAwait(false);
    }

    private static List<long> Match(Document target, List<ElementId> chunk, long filterId)
    {
        return target.GetElement(RevitIds.From(filterId)) switch
        {
            ParameterFilterElement rule when rule.GetElementFilter() is { } elementFilter =>
                RevitIds.ValuesOf(new FilteredElementCollector(target, chunk).WherePasses(elementFilter).ToElementIds()),
            ParameterFilterElement or SelectionFilterElement => RevitIds.ValuesOf(chunk),
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
                return [.. RevitIds.ValuesOf(view.GetFilters())];
            }

            return LinkedViewFilters(view, resolved);
        }
        catch (Exception)
        {
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

        return [.. RevitIds.ValuesOf(linkedView.GetFilters())];
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
                ElementParameterFilter { Inverted: true } => new UnsupportedRule(InvertedFilter),
                ElementParameterFilter parameters => ReadRules(parameters.GetRules()),
                _ => new UnsupportedRule(filter.GetType().Name)
            };
        }

        private FilterNode ReadRules(IList<RevitFilterRule> rules)
        {
            var nodes = rules.Select(rule => ReadRule(rule, false)).ToList();
            return nodes.Count == 1 ? nodes[0] : new FilterSet(SetMatch.All, nodes);
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
            var key = ParameterResolver.KeyOf(document, parameterId);
            return rule switch
            {
                HasValueFilterRule => new RuleNode(key, KindOf(parameterId),
                    negated ? FilterOperator.HasNoValue : FilterOperator.HasValue),
                HasNoValueFilterRule => new RuleNode(key, KindOf(parameterId),
                    negated ? FilterOperator.HasValue : FilterOperator.HasNoValue),
                FilterStringRule text when StringOperator(text.GetEvaluator(), negated) is { } op =>
                    new RuleNode(key, ValueKind.Text, op, text.RuleString),
                FilterIntegerRule integer when NumericOperator(integer.GetEvaluator(), negated) is { } op =>
                    new RuleNode(key, ValueKind.Integer, op,
                        integer.RuleValue.ToString(CultureInfo.CurrentCulture)),
                FilterDoubleRule number when NumericOperator(number.GetEvaluator(), negated) is { } op =>
                    new RuleNode(key, ValueKind.Number, op, FormatNumber(parameterId, key, number.RuleValue)),
                FilterElementIdRule reference when NumericOperator(reference.GetEvaluator(), negated) is
                { } op and (FilterOperator.Equal or FilterOperator.NotEqual) =>
                    new RuleNode(key, ValueKind.ElementId, op, ReferenceText(reference.RuleValue)),
                _ => new UnsupportedRule(rule.GetType().Name)
            };
        }

        private ValueKind KindOf(ElementId parameterId)
        {
            return ParameterResolver.InfoOf(document, parameterId)?.Kind ?? ValueKind.Text;
        }

        private string FormatNumber(ElementId parameterId, ParameterKey key, double value)
        {
            try
            {
                if (ParameterResolver.SpecOf(document, parameterId, key, categories) is { } spec
                    && UnitUtils.IsMeasurableSpec(spec))
                {
                    return UnitFormatUtils.Format(document.GetUnits(), spec, value, false);
                }
            }
            catch (Exception)
            {
            }

            return value.ToString(CultureInfo.CurrentCulture);
        }

        private string ReferenceText(ElementId id)
        {
            return document.GetElement(id)?.Name ?? RevitIds.ValueOf(id).ToString(CultureInfo.InvariantCulture);
        }

        private static FilterOperator? StringOperator(FilterStringRuleEvaluator evaluator, bool negated)
        {
            return evaluator switch
            {
                FilterStringEquals => negated ? FilterOperator.NotEqual : FilterOperator.Equal,
                FilterStringContains => negated ? FilterOperator.NotContains : FilterOperator.Contains,
                FilterStringBeginsWith => negated ? FilterOperator.NotBeginsWith : FilterOperator.BeginsWith,
                FilterStringEndsWith => negated ? FilterOperator.NotEndsWith : FilterOperator.EndsWith,
                FilterStringGreater => negated ? FilterOperator.LessOrEqual : FilterOperator.Greater,
                FilterStringGreaterOrEqual => negated ? FilterOperator.Less : FilterOperator.GreaterOrEqual,
                FilterStringLess => negated ? FilterOperator.GreaterOrEqual : FilterOperator.Less,
                FilterStringLessOrEqual => negated ? FilterOperator.Greater : FilterOperator.LessOrEqual,
                _ => null
            };
        }

        private static FilterOperator? NumericOperator(FilterNumericRuleEvaluator evaluator, bool negated)
        {
            return evaluator switch
            {
                FilterNumericEquals => negated ? FilterOperator.NotEqual : FilterOperator.Equal,
                FilterNumericGreater => negated ? FilterOperator.LessOrEqual : FilterOperator.Greater,
                FilterNumericGreaterOrEqual => negated ? FilterOperator.Less : FilterOperator.GreaterOrEqual,
                FilterNumericLess => negated ? FilterOperator.GreaterOrEqual : FilterOperator.Less,
                FilterNumericLessOrEqual => negated ? FilterOperator.Greater : FilterOperator.LessOrEqual,
                _ => null
            };
        }
    }
}
