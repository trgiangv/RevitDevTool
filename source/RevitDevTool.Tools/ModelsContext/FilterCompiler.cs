using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using Nice3point.Revit.Extensions;
using System.Globalization;
using RevitFilterRule = Autodesk.Revit.DB.FilterRule;
using RuleNode = DevTools.ModelsContext.Model.FilterRule;

namespace RevitDevTool.Tools.ModelsContext;

internal abstract record Compiled
{
    public static Compiled All { get; } = new MatchAll();

    public static Compiled None { get; } = new MatchNone();

    public static Compiled And(IEnumerable<Compiled> parts)
    {
        var filters = new List<ElementFilter>();
        foreach (var part in parts)
        {
            switch (part)
            {
                case MatchNone:
                    return None;
                case Filter filter:
                    filters.Add(filter.Value);
                    break;
            }
        }

        return filters.Count switch
        {
            0 => All,
            1 => new Filter(filters[0]),
            _ => new Filter(new LogicalAndFilter(filters))
        };
    }

    public static Compiled Or(IEnumerable<Compiled> parts)
    {
        var filters = new List<ElementFilter>();
        foreach (var part in parts)
        {
            switch (part)
            {
                case MatchAll:
                    return All;
                case Filter filter:
                    filters.Add(filter.Value);
                    break;
            }
        }

        return filters.Count switch
        {
            0 => None,
            1 => new Filter(filters[0]),
            _ => new Filter(new LogicalOrFilter(filters))
        };
    }

    public static Compiled Not(Compiled compiled)
    {
        return compiled switch
        {
            MatchAll => None,
            MatchNone => All,
            Filter filter => new Filter(Invert(filter.Value)),
            _ => compiled
        };
    }

    public bool Passes(Element element)
    {
        return this switch
        {
            MatchAll => true,
            Filter filter => filter.Value.PassesFilter(element),
            _ => false
        };
    }

    private static ElementFilter Invert(ElementFilter filter)
    {
        return filter switch
        {
            ElementParameterFilter parameter => new ElementParameterFilter(parameter.GetRules(), !parameter.Inverted),
            LogicalAndFilter and => new LogicalOrFilter(and.GetFilters().Select(Invert).ToList()),
            LogicalOrFilter or => new LogicalAndFilter(or.GetFilters().Select(Invert).ToList()),
            _ => throw new NotSupportedException(filter.GetType().Name)
        };
    }

    public sealed record MatchAll : Compiled;

    public sealed record MatchNone : Compiled;

    public sealed record Filter(ElementFilter Value) : Compiled;
}

internal sealed class FilterPlan(Compiled native, Func<Element, bool>? exact)
{
    public Compiled Native { get; } = native;

    public List<long> Match(Document target, List<ElementId> chunk)
    {
        if (chunk.Count == 0)
        {
            return [];
        }

        ICollection<ElementId> ids = Native switch
        {
            Compiled.MatchNone => new List<ElementId>(),
            Compiled.Filter matcher => new FilteredElementCollector(target, chunk)
                .WherePasses(matcher.Value)
                .ToElementIds(),
            _ => chunk
        };
        return exact is null
            ? ids.Select(id => id.ToLong()).ToList()
            : ids.Where(id => target.GetElement(id) is { } element && exact(element)).Select(id => id.ToLong()).ToList();
    }
}

internal sealed class FilterCompiler(
    Document target,
    Units hostUnits,
    ICollection<ElementId> categories,
    Func<ResolvedParameter, IReadOnlyDictionary<string, int>> enumValues,
    Func<ParameterKey, IReadOnlyCollection<long>?> knownIds)
{
    private const double Tolerance = 1e-6;

    private readonly Dictionary<ParameterKey, IReadOnlyList<ResolvedParameter>> _parameters = [];
    private readonly Dictionary<RuleNode, Compiled> _rules = [];

    public FilterPlan Plan(FilterSet root)
    {
        if (!CaseSensitiveRules.Any(root))
        {
            return new FilterPlan(Compile(root, false), null);
        }

        var exact = Exact(root);
        return new FilterPlan(Compile(CaseSensitiveRules.Widen(root), false), exact);
    }

    private Compiled Compile(FilterNode node, bool negate)
    {
        return node switch
        {
            FilterSet set => CompileSet(set, negate),
            RuleNode rule => negate ? Compiled.Not(Leaf(rule)) : Leaf(rule),
            _ => negate ? Compiled.None : Compiled.All
        };
    }

    private Compiled CompileSet(FilterSet set, bool negate)
    {
        var inverted = negate != set.Inverted;
        if (set.Children.Count == 0)
        {
            return inverted ? Compiled.None : Compiled.All;
        }

        var parts = set.Children.Select(child => Compile(child, inverted)).ToList();
        return (set.Match == SetMatch.All) != inverted ? Compiled.And(parts) : Compiled.Or(parts);
    }

    private Func<Element, bool> Exact(FilterNode node)
    {
        switch (node)
        {
            case FilterSet set:
                {
                    var children = set.Children.Select(Exact).ToList();
                    var all = set.Match == SetMatch.All;
                    return element => set.Inverted != (children.Count == 0
                        || (all ? children.All(child => child(element)) : children.Any(child => child(element))));
                }
            case RuleNode rule when CaseSensitiveRules.IsCaseSensitive(rule)
                                    && Resolve(rule.Parameter).Where(parameter => parameter.Storage == StorageType.String)
                                        .ToList() is { Count: > 0 } texts:
                return element =>
                {
                    var value = texts.Select(parameter => ParameterResolver.Find(element, parameter.Key, parameter.Id))
                        .FirstOrDefault(found => found is not null);
                    return TextRules.Matches(rule.Operator, value is not null, value?.AsString(), rule.Value, true);
                };
            case RuleNode rule:
                {
                    var leaf = Leaf(rule);
                    return leaf.Passes;
                }
            default:
                return _ => true;
        }
    }

    private Compiled Leaf(RuleNode rule)
    {
        if (!_rules.TryGetValue(rule, out var compiled))
        {
            compiled = Build(rule);
            _rules.Add(rule, compiled);
        }

        return compiled;
    }

    private Compiled Build(RuleNode rule)
    {
        var parameters = Resolve(rule.Parameter);
        if (parameters.Count == 0)
        {
            return rule.Operator == FilterOperator.Missing ? Compiled.All : Compiled.None;
        }

        return rule.Operator == FilterOperator.Missing
            ? Compiled.Not(Compiled.Or(parameters.Select(parameter => Exists(parameter.Id))))
            : Compiled.Or(parameters.Select(parameter => Compiled.And([Exists(parameter.Id), Build(parameter, rule)])));
    }

    private static Compiled Exists(ElementId id)
    {
        return Compiled.Or([
            Pass(ParameterFilterRuleFactory.CreateHasValueParameterRule(id)),
            Pass(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(id))
        ]);
    }

    private Compiled Build(ResolvedParameter parameter, RuleNode rule)
    {
        var id = parameter.Id;
        switch (rule.Operator)
        {
            case FilterOperator.HasValue:
                return Pass(ParameterFilterRuleFactory.CreateHasValueParameterRule(id));
            case FilterOperator.HasNoValue:
                return Pass(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(id));
            case FilterOperator.Exists:
                return Exists(id);
        }

        return parameter.Storage switch
        {
            StorageType.String => Text(id, rule),
            StorageType.Integer when parameter.IsYesNo => YesNo(id, rule),
            StorageType.Integer => Integer(parameter, rule),
            StorageType.Double => Number(parameter, rule),
            StorageType.ElementId => Reference(id, rule),
            _ => Compiled.None
        };
    }

    private static Compiled Pass(RevitFilterRule rule, bool inverted = false)
    {
        return new Compiled.Filter(new ElementParameterFilter(rule, inverted));
    }

    private static Compiled Text(ElementId parameter, RuleNode rule)
    {
        var value = rule.Value;
        RevitFilterRule? created = rule.Operator switch
        {
            FilterOperator.Equal => ParameterFilterRuleFactory.CreateEqualsRule(parameter, value),
            FilterOperator.NotEqual => ParameterFilterRuleFactory.CreateNotEqualsRule(parameter, value),
            FilterOperator.Greater => ParameterFilterRuleFactory.CreateGreaterRule(parameter, value),
            FilterOperator.GreaterOrEqual => ParameterFilterRuleFactory.CreateGreaterOrEqualRule(parameter, value),
            FilterOperator.Less => ParameterFilterRuleFactory.CreateLessRule(parameter, value),
            FilterOperator.LessOrEqual => ParameterFilterRuleFactory.CreateLessOrEqualRule(parameter, value),
            FilterOperator.Contains => ParameterFilterRuleFactory.CreateContainsRule(parameter, value),
            FilterOperator.NotContains => ParameterFilterRuleFactory.CreateNotContainsRule(parameter, value),
            FilterOperator.BeginsWith => ParameterFilterRuleFactory.CreateBeginsWithRule(parameter, value),
            FilterOperator.NotBeginsWith => ParameterFilterRuleFactory.CreateNotBeginsWithRule(parameter, value),
            FilterOperator.EndsWith => ParameterFilterRuleFactory.CreateEndsWithRule(parameter, value),
            FilterOperator.NotEndsWith => ParameterFilterRuleFactory.CreateNotEndsWithRule(parameter, value),
            _ => null
        };
        return created is null ? Compiled.None : Pass(created);
    }

    private static Compiled YesNo(ElementId parameter, RuleNode rule)
    {
        return ValueKinds.TryParseYesNo(rule.Value, out var value) ? IntegerRule(parameter, rule.Operator, value) : Compiled.None;
    }

    private Compiled Integer(ResolvedParameter parameter, RuleNode rule)
    {
        var text = rule.Value.Trim();
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value)
            || int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return IntegerRule(parameter.Id, rule.Operator, value);
        }

        if (enumValues(parameter).TryGetValue(text, out value))
        {
            return IntegerRule(parameter.Id, rule.Operator, value);
        }

        return rule.Operator == FilterOperator.NotEqual
            ? Pass(ParameterFilterRuleFactory.CreateHasValueParameterRule(parameter.Id))
            : Compiled.None;
    }

    private static Compiled IntegerRule(ElementId parameter, FilterOperator op, int value)
    {
        RevitFilterRule? created = op switch
        {
            FilterOperator.Equal => ParameterFilterRuleFactory.CreateEqualsRule(parameter, value),
            FilterOperator.NotEqual => ParameterFilterRuleFactory.CreateNotEqualsRule(parameter, value),
            FilterOperator.Greater => ParameterFilterRuleFactory.CreateGreaterRule(parameter, value),
            FilterOperator.GreaterOrEqual => ParameterFilterRuleFactory.CreateGreaterOrEqualRule(parameter, value),
            FilterOperator.Less => ParameterFilterRuleFactory.CreateLessRule(parameter, value),
            FilterOperator.LessOrEqual => ParameterFilterRuleFactory.CreateLessOrEqualRule(parameter, value),
            _ => null
        };
        return created is null ? Compiled.None : Pass(created);
    }

    private Compiled Number(ResolvedParameter parameter, RuleNode rule)
    {
        if (!TryParseNumber(parameter, rule.Value, out var value))
        {
            return Compiled.None;
        }

        var id = parameter.Id;
        RevitFilterRule? created = rule.Operator switch
        {
            FilterOperator.Equal => ParameterFilterRuleFactory.CreateEqualsRule(id, value, Tolerance),
            FilterOperator.NotEqual => ParameterFilterRuleFactory.CreateNotEqualsRule(id, value, Tolerance),
            FilterOperator.Greater => ParameterFilterRuleFactory.CreateGreaterRule(id, value, Tolerance),
            FilterOperator.GreaterOrEqual => ParameterFilterRuleFactory.CreateGreaterOrEqualRule(id, value, Tolerance),
            FilterOperator.Less => ParameterFilterRuleFactory.CreateLessRule(id, value, Tolerance),
            FilterOperator.LessOrEqual => ParameterFilterRuleFactory.CreateLessOrEqualRule(id, value, Tolerance),
            _ => null
        };
        return created is null ? Compiled.None : Pass(created);
    }

    private Compiled Reference(ElementId parameter, RuleNode rule)
    {
        var targets = ReferenceTargets(rule.Value.Trim());
        return rule.Operator switch
        {
            FilterOperator.Equal => Compiled.Or(targets.Select(item =>
                Pass(ParameterFilterRuleFactory.CreateEqualsRule(parameter, item)))),
            FilterOperator.NotEqual => Compiled.And(targets.Select(item =>
                    Pass(ParameterFilterRuleFactory.CreateNotEqualsRule(parameter, item)))
                .Prepend(Pass(ParameterFilterRuleFactory.CreateHasValueParameterRule(parameter)))),
            _ => Compiled.None
        };
    }

    private List<ElementId> ReferenceTargets(string value)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            return [id.ToElementId()];
        }

        var named = new List<ElementId>();
        foreach (var type in new[] { typeof(Level), typeof(Phase), typeof(Material) })
        {
            named.AddRange(new FilteredElementCollector(target)
                .OfClass(type)
                .Where(element => string.Equals(element.Name, value, StringComparison.Ordinal))
                .Select(element => element.Id));
        }

        if (named.Count == 0)
        {
            named.AddRange(new FilteredElementCollector(target)
                .WhereElementIsElementType()
                .Where(element => string.Equals(element.Name, value, StringComparison.Ordinal))
                .Select(element => element.Id));
        }

        return named;
    }

    private bool TryParseNumber(ResolvedParameter parameter, string text, out double value)
    {
        var trimmed = text.Trim();
        if (parameter.Spec is { } spec && ParameterResolver.IsMeasurable(spec))
        {
            try
            {
                if (UnitFormatUtils.TryParse(hostUnits, spec, trimmed, out value))
                {
                    return true;
                }
            }
            catch (Exception exception)
            {
                RevitProfiler.Logger.Swallowed(exception, "Parse number", parameter.Key.Name);
            }
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
               || double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private IReadOnlyList<ResolvedParameter> Resolve(ParameterKey key)
    {
        if (!_parameters.TryGetValue(key, out var parameters))
        {
            parameters = ParameterResolver.Resolve(target, key, categories, knownIds(key));
            _parameters.Add(key, parameters);
        }

        return parameters;
    }
}
