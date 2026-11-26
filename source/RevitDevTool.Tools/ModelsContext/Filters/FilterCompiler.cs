using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using RevitDevTool.Tools.ModelsContext.Pane;
using RevitDevTool.Tools.ModelsContext.Parameters;
using System.Globalization;
using RuleNode = DevTools.ModelsContext.Filters.FilterRule;

namespace RevitDevTool.Tools.ModelsContext.Filters;

internal sealed class FilterCompiler(
    string document,
    Func<ParameterKey, IReadOnlyList<ResolvedParameter>> resolve,
    Func<ParameterKey, IReadOnlyDictionary<string, long[]>?> rawValues,
    RuleValueParser parser,
    TypeFilters types)
{
    private readonly Dictionary<RuleNode, Compiled> _rules = [];
    private bool _ordersText;

    public HashSet<ParameterKey> MissingValues { get; } = [];

    public int Rules => _rules.Count;

    public int Unresolved { get; private set; }

    public FilterPlan Plan(FilterSet root)
    {
        var caseSensitive = CaseSensitiveRules.Any(root);
        var native = Compile(caseSensitive ? CaseSensitiveRules.Widen(root) : root);
        return caseSensitive || _ordersText
            ? new FilterPlan(native, ExactPredicate.Build(root, resolve, Leaf))
            : new FilterPlan(native, null);
    }

    private Compiled Compile(FilterNode node)
    {
        return node switch
        {
            FilterSet set => Compiled.And([RevitRuleFactory.InCategories(set.Scope), CompileSet(set)]),
            RuleNode rule => Leaf(rule),
            _ => Compiled.All
        };
    }

    private Compiled CompileSet(FilterSet set)
    {
        if (set.Children.Count == 0)
        {
            return Compiled.All;
        }

        var parts = set.Children.Select(Compile).ToList();
        return set.Match == SetMatch.All ? Compiled.And(parts) : Compiled.Or(parts);
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
        var parameters = resolve(rule.Parameter);
        if (parameters.Count == 0)
        {
            if (rule.Operator == FilterOperator.Undefined)
            {
                return Compiled.All;
            }

            Unresolved++;
            RevitProfiler.Logger.RuleUnresolved(document, rule.Parameter.Name);
            return Compiled.None;
        }

        return rule.Operator switch
        {
            FilterOperator.Undefined => Compiled.And(parameters.Select(Missing)),
            FilterOperator.Empty when parameters.Count > 1 => Compiled.And([
                Compiled.Or(parameters.Select(parameter => Presence(parameter, FilterOperator.Empty))),
                .. parameters.Select(parameter => Compiled.Or([Presence(parameter, FilterOperator.Empty), Missing(parameter)]))
            ]),
            FilterOperator.Defined or FilterOperator.NotEmpty or FilterOperator.Empty =>
                Compiled.Or(parameters.Select(parameter => Presence(parameter, rule.Operator))),
            FilterOperator.NotEqual => Compiled.Or(parameters.Select(parameter => Compiled.And([
                Presence(parameter, FilterOperator.Defined),
                Compiled.Or([Presence(parameter, FilterOperator.Empty), Build(parameter, rule)])
            ]))),
            _ => Compiled.Or(parameters.Select(parameter =>
                Compiled.And([Presence(parameter, Guard(parameter)), Build(parameter, rule)])))
        };
    }

    private static FilterOperator Guard(ResolvedParameter parameter)
    {
        return parameter.Storage == StorageType.String ? FilterOperator.Defined : FilterOperator.NotEmpty;
    }

    private Compiled Presence(ResolvedParameter parameter, FilterOperator op)
    {
        var id = parameter.ElementId;
        var hasValue = RevitRuleFactory.HasValue(id, parameter.Storage);
        var hasNoValue = Carried(parameter, RevitRuleFactory.HasNoValue(id, parameter.Storage));
        var holds = HeldBy(parameter);
        var exists = Compiled.Or([RevitRuleFactory.HasValue(id), Carried(parameter, RevitRuleFactory.HasNoValue(id))]);
        var held = parameter.Held == HeldMeaning.Value
            ? Compiled.And([holds, Compiled.Not(RevitRuleFactory.HasNoValue(id))])
            : Compiled.None;
        var heldEmpty = parameter.Held == HeldMeaning.Empty
            ? Compiled.And([holds, Compiled.Not(RevitRuleFactory.HasValue(id)), Compiled.Not(RevitRuleFactory.HasNoValue(id))])
            : Compiled.None;
        var silentFilled = types.Only(parameter.Silent.Filled);
        var silentEmpty = types.Only(parameter.Silent.Empty);
        if (parameter.Types.IsEmpty)
        {
            return op switch
            {
                FilterOperator.NotEmpty => Compiled.Or([hasValue, held, silentFilled]),
                FilterOperator.Empty => Compiled.Or([hasNoValue, heldEmpty, silentEmpty]),
                _ => Compiled.Or([exists, holds, silentFilled, silentEmpty])
            };
        }

        var defined = parameter.Types.TypesFor(FilterOperator.Defined);
        return op switch
        {
            FilterOperator.NotEmpty => Compiled.Or([hasValue, types.In(parameter.Types.WithValue),
                Compiled.And([held, types.NotIn(defined)]), silentFilled]),
            FilterOperator.Empty => Compiled.Or([
                Compiled.And([hasNoValue, types.NotIn(parameter.Types.WithValue)]),
                Compiled.And([Compiled.Not(RevitRuleFactory.HasValue(id)), types.In(parameter.Types.WithoutValue)]),
                Compiled.And([heldEmpty, types.NotIn(defined)]),
                silentEmpty
            ]),
            _ => Compiled.Or([exists, types.In(defined), holds, silentFilled, silentEmpty])
        };
    }

    private static Compiled HeldBy(ResolvedParameter parameter)
    {
        return parameter.Held == HeldMeaning.Absent ? Compiled.None : RevitRuleFactory.Holds(parameter.ElementId, parameter.Storage);
    }

    private Compiled Carried(ResolvedParameter parameter, Compiled rule)
    {
        return parameter.Lacking.Count == 0 ? rule : Compiled.And([rule, types.Except(parameter.Lacking)]);
    }

    private Compiled Missing(ResolvedParameter parameter)
    {
        var id = parameter.ElementId;
        var own = Compiled.And([
            Compiled.Or([
                Compiled.And([Compiled.Not(RevitRuleFactory.Exists(id)), Compiled.Not(HeldBy(parameter))]),
                types.Only(parameter.Lacking)
            ]),
            types.Except(parameter.Silent.All)
        ]);
        return parameter.Types.IsEmpty
            ? own
            : Compiled.And([own, types.NotIn(parameter.Types.TypesFor(FilterOperator.Defined))]);
    }

    private Compiled Build(ResolvedParameter parameter, RuleNode rule)
    {
        var id = parameter.ElementId;
        return parameter.Storage switch
        {
            StorageType.String when TextRules.IsOrder(rule.Operator) => OrdersText(),
            StorageType.String => RevitRuleFactory.Text(id, rule.Operator, rule.Value),
            StorageType.Integer when parameter.IsYesNo => YesNo(id, rule),
            StorageType.Integer => Integer(parameter, rule),
            StorageType.Double when rule.Picked is { } picked => Number(id, rule.Operator, RuleValueParser.Picked(picked)),
            StorageType.Double => parser.TryNumber(parameter, rule.Value, out var number)
                ? Number(id, rule.Operator, number)
                : Compiled.None,
            StorageType.ElementId => Reference(parameter, rule),
            _ => Compiled.None
        };
    }

    private static Compiled Number(ElementId id, FilterOperator op, RuleNumber number)
    {
        return RevitRuleFactory.Number(id, op, number.Value, number.Tolerance);
    }

    private Compiled Reference(ResolvedParameter parameter, RuleNode rule)
    {
        if (RawValuesOf(parameter) is not { } values)
        {
            return Compiled.None;
        }

        var text = rule.Value.Trim();
        long[] targets = RawValueNames.TryFind(values, text, out var named) ? named
            : long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? [id]
            : [];
        return RevitRuleFactory.Reference(parameter.ElementId, rule.Operator, targets);
    }

    private Compiled OrdersText()
    {
        _ordersText = true;
        return Compiled.All;
    }

    private IReadOnlyDictionary<string, long[]>? RawValuesOf(ResolvedParameter parameter)
    {
        if (rawValues(parameter.Key) is { } values)
        {
            return values;
        }

        MissingValues.Add(parameter.Key);
        return null;
    }

    private static Compiled YesNo(ElementId parameter, RuleNode rule)
    {
        return ValueKinds.TryParseYesNo(rule.Value, out var value)
            ? RevitRuleFactory.Integer(parameter, rule.Operator, value)
            : Compiled.None;
    }

    private Compiled Integer(ResolvedParameter parameter, RuleNode rule)
    {
        if (RuleValueParser.TryInteger(rule.Value, out var value))
        {
            return RevitRuleFactory.Integer(parameter.ElementId, rule.Operator, value);
        }

        if (RawValuesOf(parameter) is not { } names)
        {
            return Compiled.None;
        }

        if (RawValueNames.TryFind(names, rule.Value, out var raws))
        {
            var rules = raws.Select(raw => RevitRuleFactory.Integer(parameter.ElementId, rule.Operator, (int)raw));
            return rule.Operator == FilterOperator.NotEqual ? Compiled.And(rules) : Compiled.Or(rules);
        }

        return rule.Operator == FilterOperator.NotEqual ? Compiled.All : Compiled.None;
    }
}
