using DevTools.ModelsContext.Model;
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

    public sealed record MatchAll : Compiled;

    public sealed record MatchNone : Compiled;

    public sealed record Filter(ElementFilter Value) : Compiled;
}

internal sealed class FilterCompiler(Document document, ICollection<ElementId> categories)
{
    private const double Tolerance = 1e-6;

    private readonly Dictionary<ParameterKey, ElementId?> _parameters = [];

    public Compiled Compile(FilterSet set)
    {
        if (set.Children.Count == 0)
        {
            return Compiled.All;
        }

        var parts = set.Children.Select(Compile).ToList();
        return set.Match == SetMatch.All ? Compiled.And(parts) : Compiled.Or(parts);
    }

    private Compiled Compile(FilterNode node)
    {
        return node switch
        {
            FilterSet set => Compile(set),
            RuleNode rule => Compile(rule),
            _ => Compiled.All
        };
    }

    private Compiled Compile(RuleNode rule)
    {
        var parameterId = ParameterId(rule.Parameter);
        if (parameterId is null)
        {
            return rule.Operator == FilterOperator.Missing ? Compiled.All : Compiled.None;
        }

        switch (rule.Operator)
        {
            case FilterOperator.HasValue:
                return Pass(ParameterFilterRuleFactory.CreateHasValueParameterRule(parameterId));
            case FilterOperator.HasNoValue:
                return Pass(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(parameterId));
            case FilterOperator.Exists:
                return Compiled.Or([
                    Pass(ParameterFilterRuleFactory.CreateHasValueParameterRule(parameterId)),
                    Pass(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(parameterId))
                ]);
            case FilterOperator.Missing:
                return Compiled.And([
                    Pass(ParameterFilterRuleFactory.CreateHasValueParameterRule(parameterId), true),
                    Pass(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(parameterId), true)
                ]);
        }

        return rule.Kind switch
        {
            ValueKind.Text => Text(parameterId, rule),
            ValueKind.Integer => Integer(parameterId, rule),
            ValueKind.Number => Number(parameterId, rule),
            _ => Reference(parameterId, rule)
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

    private static Compiled Integer(ElementId parameter, RuleNode rule)
    {
        if (!TryParseInteger(rule.Value, out var value))
        {
            return Compiled.None;
        }

        RevitFilterRule? created = rule.Operator switch
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

    private Compiled Number(ElementId parameter, RuleNode rule)
    {
        if (!TryParseNumber(parameter, rule, out var value))
        {
            return Compiled.None;
        }

        RevitFilterRule? created = rule.Operator switch
        {
            FilterOperator.Equal => ParameterFilterRuleFactory.CreateEqualsRule(parameter, value, Tolerance),
            FilterOperator.NotEqual => ParameterFilterRuleFactory.CreateNotEqualsRule(parameter, value, Tolerance),
            FilterOperator.Greater => ParameterFilterRuleFactory.CreateGreaterRule(parameter, value, Tolerance),
            FilterOperator.GreaterOrEqual => ParameterFilterRuleFactory.CreateGreaterOrEqualRule(parameter, value,
                Tolerance),
            FilterOperator.Less => ParameterFilterRuleFactory.CreateLessRule(parameter, value, Tolerance),
            FilterOperator.LessOrEqual => ParameterFilterRuleFactory.CreateLessOrEqualRule(parameter, value,
                Tolerance),
            _ => null
        };
        return created is null ? Compiled.None : Pass(created);
    }

    private Compiled Reference(ElementId parameter, RuleNode rule)
    {
        var targets = ReferenceTargets(rule.Value);
        return rule.Operator switch
        {
            FilterOperator.Equal => Compiled.Or(targets.Select(target =>
                Pass(ParameterFilterRuleFactory.CreateEqualsRule(parameter, target)))),
            FilterOperator.NotEqual => Compiled.And(targets.Select(target =>
                    Pass(ParameterFilterRuleFactory.CreateNotEqualsRule(parameter, target)))
                .Prepend(Pass(ParameterFilterRuleFactory.CreateHasValueParameterRule(parameter)))),
            _ => Compiled.None
        };
    }

    private List<ElementId> ReferenceTargets(string value)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            return [RevitIds.From(id)];
        }

        var named = new List<ElementId>();
        foreach (var type in new[] { typeof(Level), typeof(Phase), typeof(Material) })
        {
            named.AddRange(new FilteredElementCollector(document)
                .OfClass(type)
                .Where(element => string.Equals(element.Name, value, StringComparison.Ordinal))
                .Select(element => element.Id));
        }

        if (named.Count == 0)
        {
            named.AddRange(new FilteredElementCollector(document)
                .WhereElementIsElementType()
                .Where(element => string.Equals(element.Name, value, StringComparison.Ordinal))
                .Select(element => element.Id));
        }

        return named;
    }

    private bool TryParseNumber(ElementId parameter, RuleNode rule, out double value)
    {
        var text = rule.Value.Trim();
        if (ParameterResolver.SpecOf(document, parameter, rule.Parameter, categories) is { } spec)
        {
            try
            {
                if (UnitUtils.IsMeasurableSpec(spec) && UnitFormatUtils.TryParse(document.GetUnits(), spec, text, out value))
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
               || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseInteger(string text, out int value)
    {
        var trimmed = text.Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }

        if (string.Equals(trimmed, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase))
        {
            value = 1;
            return true;
        }

        if (string.Equals(trimmed, "no", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase))
        {
            value = 0;
            return true;
        }

        return false;
    }

    private ElementId? ParameterId(ParameterKey key)
    {
        if (!_parameters.TryGetValue(key, out var id))
        {
            id = ParameterResolver.IdOf(document, key);
            _parameters.Add(key, id);
        }

        return id;
    }
}
